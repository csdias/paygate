using Dapper;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.TestData;

public class PostgresMessageProcessorTestData
{
    private readonly IOutboxTableNames _tableNames;

    public PostgresMessageProcessorTestData(IOutboxTableNames tableNames)
    {
        _tableNames = tableNames;
    }

    public OutboxMessage BuildOutboxMessage(MessageRegistry registry, Guid? predecessorId = null)
        => new()
        {
            MessageId = Guid.NewGuid(),
            MessageBody = """{"paymentId":"abc-123","amount":100}""",
            ContextId = Guid.NewGuid().ToString(),
            PredecessorId = predecessorId,
            OccurredAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            MessageFilters = Array.Empty<MessageFilter>(),
            MessageRegistry = registry
        };

    public OutboxMessage BuildOutboxMessageWithFilters(MessageRegistry registry)
        => new()
        {
            MessageId = Guid.NewGuid(),
            MessageBody = """{"paymentId":"filter-test","amount":200}""",
            ContextId = Guid.NewGuid().ToString(),
            OccurredAt = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            MessageFilters =
            [
                new MessageFilter { FilterKey = "tenant", FilterValue = "acme" },
                new MessageFilter { FilterKey = "environment", FilterValue = "production" }
            ],
            MessageRegistry = registry
        };

    public async Task InsertOutboxMessageAsync(Npgsql.NpgsqlConnection connection, OutboxMessage message)
    {
        await connection.ExecuteAsync($@"
INSERT INTO {_tableNames.Outbox}
    (message_id, message_registry_id, message_body, context_id, predecessor_id, occurred_at, last_updated, retry_count)
VALUES
    (@MessageId, @RegistryId, @MessageBody::json, @ContextId, @PredecessorId, @OccurredAt, @LastUpdated, 0)",
            new
            {
                message.MessageId,
                RegistryId = message.MessageRegistry.MessageRegistryId,
                message.MessageBody,
                message.ContextId,
                message.PredecessorId,
                message.OccurredAt,
                message.LastUpdated
            });

        foreach (var filter in message.MessageFilters)
        {
            await connection.ExecuteAsync($@"
INSERT INTO {_tableNames.MessageFilter}
    (message_filter_id, message_id, filter_key, filter_value)
VALUES
    (@FilterId, @MessageId, @FilterKey, @FilterValue)",
                new
                {
                    FilterId = Guid.NewGuid(),
                    message.MessageId,
                    filter.FilterKey,
                    filter.FilterValue
                });
        }
    }
}
