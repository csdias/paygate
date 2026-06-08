using Dapper;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient.Postgres;

public class PostgresOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly IDbContext _context;
    private readonly IOutboxTableNames _tableNames;

    public PostgresOutboxMessageRepository(IDbContext context, IOutboxTableNames tableNames)
    {
        _context = context;
        _tableNames = tableNames;
    }

    public async Task<Guid> CreateOutboxMessage(OutboxMessage outboxMessage)
    {
        var id = Guid.NewGuid();
        var sql = $@"
INSERT INTO {_tableNames.Outbox} (message_id, message_registry_id, context_id, occurred_at, message, last_updated)
VALUES (:messageId, :messageRegistryId, :contextId, :occurredAt, :message::json, :lastUpdated)
";
        await _context.DbConnection.ExecuteAsync(sql, new
        {
            messageId = id,
            messageRegistryId = outboxMessage.MessageRegistryId,
            contextId = outboxMessage.ContextId,
            occurredAt = outboxMessage.OccurredAt,
            message = outboxMessage.MessageBody,
            lastUpdated = outboxMessage.LastUpdated
        }, _context.CurrentTransaction);

        return id;
    }

    public async Task<OutboxMessage> GetById(Guid id)
    {
        var sql = $@"
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
    f.filter_value as FilterValue,

    p.predecessor_id as PredecessorId
FROM {_tableNames.Outbox} o
    INNER JOIN {_tableNames.MessageRegistry} r ON o.message_registry_id = r.message_registry_id
    INNER JOIN {_tableNames.MessageType} t     ON r.message_type_id = t.message_type_id
    LEFT JOIN {_tableNames.MessageFilter} f    ON o.message_id = f.message_id
    LEFT JOIN LATERAL (
        SELECT o.message_id, ob2.message_id AS predecessor_id
        FROM {_tableNames.Outbox} ob2
        WHERE ob2.message_registry_id = o.message_registry_id
          AND ob2.context_id          = o.context_id
          AND ob2.message_id         != o.message_id
          AND ob2.occurred_at         < o.occurred_at
        ORDER BY ob2.occurred_at DESC
        LIMIT 1
    ) AS p ON p.message_id = o.message_id
WHERE o.message_id = :id
";
        static OutboxMessage Map(OutboxMessage outbox, MessageRegistry registry, MessageType type, MessageFilter filter)
        {
            registry.MessageType = type;
            outbox.MessageRegistry = registry;
            outbox.MessageFilters = new[] { filter };
            return outbox;
        }

        var results = await _context.DbConnection
            .QueryAsync<OutboxMessage, MessageRegistry, MessageType, MessageFilter, OutboxMessage>(
                sql, Map, param: new { id },
                splitOn: "MessageId,MessageRegistryId,MessageTypeId,MessageId");

        return results.GroupBy(r => r.MessageId).Select(g =>
        {
            var grouped = g.First();
            grouped.MessageFilters = g.Select(m => m.MessageFilters.Single()).AsList();
            return grouped;
        }).FirstOrDefault();
    }
}
