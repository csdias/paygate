using System.Diagnostics;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Npgsql;
using Paygate.Consumers.Common;

namespace Paygate.Audit;

public class Function
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
        ?? throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set.");

    /// <summary>
    /// Writes an immutable audit record for every payment event received (initiated and decided).
    /// Uses partial batch failure: a single DB error retries only the affected record.
    /// </summary>
    public async Task<SQSBatchResponse> Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        foreach (var record in sqsEvent.Records)
        {
            var traceParent = SnsMessageUnwrapper.ExtractTraceParent(record);
            using var activity = new Activity("audit.process");
            if (!string.IsNullOrEmpty(traceParent))
                activity.SetParentId(traceParent);
            activity.Start();

            try
            {
                var messageName = SnsMessageUnwrapper.ExtractMessageName(record);

                // Resolve payment id, payload and event time per event type.
                Guid paymentId;
                string payload;
                DateTimeOffset occurredAt;

                switch (messageName)
                {
                    case "PaymentInitiatedEvent":
                        var initiated = SnsMessageUnwrapper.Unwrap<PaymentInitiatedEvent>(record);
                        if (initiated is null) goto fail;
                        (paymentId, payload, occurredAt) =
                            (initiated.PaymentId, JsonSerializer.Serialize(initiated), initiated.CreatedAt);
                        break;

                    case "PaymentDecidedEvent":
                        var decided = SnsMessageUnwrapper.Unwrap<PaymentDecisionEvent>(record);
                        if (decided is null) goto fail;
                        (paymentId, payload, occurredAt) =
                            (decided.PaymentId, JsonSerializer.Serialize(decided), decided.DecidedAt);
                        break;

                    default:
                        context.Logger.LogWarning(
                            "Unknown message '{MessageName}' on {MessageId}.", messageName ?? "(none)", record.MessageId);
                        goto fail;
                }

                var auditRecord = new AuditRecord(
                    AuditId: Guid.NewGuid(),
                    PaymentId: paymentId,
                    EventType: messageName!,
                    Payload: payload,
                    OccurredAt: occurredAt);

                await new AuditRepository(connection).InsertAsync(auditRecord);

                context.Logger.LogInformation(
                    "Audit record written for payment {PaymentId} ({EventType}).", paymentId, messageName);

                continue;

                fail:
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex,
                    "Failed to write audit record for SQS message {MessageId}.", record.MessageId);
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }
}
