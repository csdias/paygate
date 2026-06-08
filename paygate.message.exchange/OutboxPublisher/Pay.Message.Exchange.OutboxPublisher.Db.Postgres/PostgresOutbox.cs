using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Db.Postgres;

public class PostgresOutbox : IOutbox
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DatabaseOptions _options;
    private readonly ILogger<PostgresOutbox> _logger;
    private readonly ITraceDataEnricher _traceDataEnricher;
    private readonly IOutboxTableNames _tables;

    public PostgresOutbox(IDbConnectionFactory connectionFactory, IOptions<DatabaseOptions> options,
        ITraceDataEnricher traceDataEnricher, ILogger<PostgresOutbox> logger, IOutboxTableNames tables)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _tables = tables;
        _traceDataEnricher = traceDataEnricher;
        _options = options.Value;
    }

    public async Task<IReadOnlyCollection<OutboxMessage>> ReserveAndFetchUnpublishedMessagesAsync(Guid runId)
    {
        static OutboxMessage Map(OutboxMessage outbox, MessageRegistry registry, Entities.MessageType type,
            MessageFilter filter)
        {
            registry.MessageType = type;
            outbox.MessageRegistry = registry;
            outbox.MessageFilters = filter != null ? new[] { filter } : Array.Empty<MessageFilter>();
            return outbox;
        }

        var selectSql = $@"
SELECT
    o.message_id          as MessageId,
    o.message_registry_id as MessageRegistryId,
    o.context_id          as ContextId,
    o.occurred_at         as OccurredAt,
    o.message             as MessageBody,
    o.published_at        as PublishedAt,
    o.last_updated        as LastUpdated,
    o.retry_count         as RetryCount,
    o.processed_by        as ProcessedBy,
    o.traceparent         as TraceParent,
    o.tracestate          as TraceState,
    p.predecessor_id      as PredecessorId,

    r.message_registry_id      as MessageRegistryId,
    r.message_name             as MessageName,
    r.message_version          as MessageVersion,
    r.message_type_id          as MessageTypeId,
    r.topic                    as Topic,
    r.retry_limit              as RetryLimit,
    r.retry_backoff_in_seconds as RetryBackoffInSeconds,

    t.message_type_id   as MessageTypeId,
    t.message_type_desc as MessageTypeDescription,

    f.message_id   as MessageId,
    f.filter_key   as FilterKey,
    f.filter_value as FilterValue
FROM {_tables.Outbox} o
    INNER JOIN {_tables.MessageRegistry} r ON o.message_registry_id = r.message_registry_id
    INNER JOIN {_tables.MessageType} t     ON r.message_type_id = t.message_type_id
    LEFT JOIN {_tables.MessageFilter} f    ON o.message_id = f.message_id
    LEFT JOIN LATERAL (
        SELECT o.message_id, ob2.message_id AS predecessor_id
        FROM {_tables.Outbox} ob2
        WHERE ob2.message_registry_id = o.message_registry_id
          AND ob2.context_id          = o.context_id
          AND ob2.message_id         != o.message_id
          AND ob2.occurred_at         < o.occurred_at
        ORDER BY ob2.occurred_at DESC
        LIMIT 1
    ) AS p ON p.message_id = o.message_id
WHERE o.published_at IS NULL
  AND (o.retry_count = 0 OR (o.retry_count <= r.retry_limit AND now() - o.last_updated > make_interval(secs := r.retry_backoff_in_seconds)))
  AND (o.processed_by IS NULL OR now() - o.last_updated > :stalledTime)
ORDER BY o.occurred_at
LIMIT :batchSize
FOR UPDATE OF o SKIP LOCKED
";

        var updateRunStateSql = $@"
UPDATE {_tables.Outbox}
SET processed_by = :runId,
    last_updated = now(),
    retry_count  = retry_count + 1
WHERE message_id = ANY(:messageIds)
";

        using var conn = _connectionFactory.GetConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            var resultUnmerged =
                await conn.QueryAsync<OutboxMessage, MessageRegistry, Entities.MessageType, MessageFilter, OutboxMessage>(
                    selectSql, Map,
                    new { batchSize = _options.BatchSize, stalledTime = _options.InactiveDelay },
                    transaction: transaction,
                    splitOn: "MessageId,MessageRegistryId,MessageTypeId,MessageId");

            var results = resultUnmerged.GroupBy(r => r.MessageId).Select(g =>
            {
                var grouped = g.First();
                grouped.ProcessedBy = runId;
                grouped.MessageFilters = g.Where(m => m.MessageFilters.Count > 0)
                    .Select(m => m.MessageFilters.Single()).AsList();
                return grouped;
            }).AsList();

            var messageIds = results.Select(x => x.MessageId).AsList();
            await EnsureTraceDataIsPresent(results, conn, transaction);
            await conn.ExecuteAsync(updateRunStateSql, new { runId, messageIds }, transaction: transaction);
            transaction.Commit();
            return results;
        }
        catch (DbException e)
        {
            _logger.LogError(e, "Caught exception while reserving messages, rolling back.");
            transaction.Rollback();
            throw;
        }
    }

    private async Task EnsureTraceDataIsPresent(IEnumerable<OutboxMessage> messages, IDbConnection connection,
        IDbTransaction transaction)
    {
        var updateSql = $@"
UPDATE {_tables.Outbox}
SET traceparent = :traceParent,
    tracestate  = :traceState
WHERE message_id = :id
";
        foreach (var message in messages)
        {
            if (_traceDataEnricher.EnrichAndUpdateMessage(message))
            {
                await connection.ExecuteAsync(updateSql,
                    new { traceParent = message.TraceParent, traceState = message.TraceState, id = message.MessageId },
                    transaction);
            }
        }
    }

    public async Task MarkMessagesAsPublishedAsync(OutboxMessage message)
    {
        var sql = $@"
UPDATE {_tables.Outbox}
SET published_at = now(),
    last_updated = now()
WHERE message_id = :id
";
        using var conn = _connectionFactory.GetConnection();
        await conn.ExecuteAsync(sql, new { id = message.MessageId });
        _logger.LogInformation("Marked message id: {MessageId} as published.", message.MessageId);
    }

    public Task MarkMessagesAsFailedAsync(OutboxMessage message)
    {
        if (message.RetryCount >= message.MessageRegistry.RetryLimit)
            _logger.LogError("Message id: {MessageId} has exceeded its retry limit and cannot be published.",
                message.MessageId);

        return Task.CompletedTask;
    }
}
