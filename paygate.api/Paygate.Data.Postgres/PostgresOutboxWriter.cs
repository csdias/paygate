using System.Data;
using System.Diagnostics;
using Dapper;
using Paygate.Data;

namespace Paygate.Data.Postgres;

public class PostgresOutboxWriter : ITransactionalOutboxWriter
{
    public async Task WriteAsync(Guid messageRegistryId, string contextId, string messageBody,
        IDbTransaction transaction)
    {
        // Column is `message` (json) — matches the publisher's read path (PostgresOutbox)
        // and the shared OutboxClient library. The API never reads the outbox back, so this
        // is the only place that referenced the message column.
        const string sql = @"
INSERT INTO outbox (message_id, message_registry_id, message, context_id, occurred_at, last_updated, retry_count, traceparent, tracestate)
VALUES (@MessageId, @MessageRegistryId, @MessageBody::json, @ContextId, @OccurredAt, @OccurredAt, 0, @TraceParent, @TraceState)";

        await transaction.Connection!.ExecuteAsync(sql, new
        {
            MessageId = Guid.NewGuid(),
            MessageRegistryId = messageRegistryId,
            MessageBody = messageBody,
            ContextId = contextId,
            OccurredAt = DateTimeOffset.UtcNow,
            TraceParent = Activity.Current?.Id,
            TraceState = Activity.Current?.TraceStateString
        }, transaction);
    }
}
