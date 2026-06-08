using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Paygate.Consumers.Common;

namespace Paygate.Notification;

public class Function
{
    /// <summary>
    /// Processes a batch of PaymentInitiatedEvent messages from the notification SQS queue.
    /// Returns an SQSBatchResponse so Lambda only retries the individual records that failed,
    /// rather than the entire batch (partial batch failure).
    /// </summary>
    public SQSBatchResponse Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            var traceParent = SnsMessageUnwrapper.ExtractTraceParent(record);
            using var activity = new Activity("notification.process");
            if (!string.IsNullOrEmpty(traceParent))
                activity.SetParentId(traceParent);
            activity.Start();

            try
            {
                var messageName = SnsMessageUnwrapper.ExtractMessageName(record);

                switch (messageName)
                {
                    case "PaymentInitiatedEvent":
                        if (!HandleInitiated(record, context)) goto fail;
                        break;
                    case "PaymentDecidedEvent":
                        if (!HandleDecided(record, context)) goto fail;
                        break;
                    default:
                        context.Logger.LogWarning(
                            "Unknown message '{MessageName}' on {MessageId} — sending to DLQ.",
                            messageName ?? "(none)", record.MessageId);
                        goto fail;
                }

                continue;

                fail:
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex,
                    "Failed to process notification for SQS message {MessageId}.", record.MessageId);
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    private static bool HandleInitiated(SQSEvent.SQSMessage record, ILambdaContext context)
    {
        var payment = SnsMessageUnwrapper.Unwrap<PaymentInitiatedEvent>(record);
        if (payment is null)
        {
            context.Logger.LogWarning("Could not deserialise PaymentInitiatedEvent from {MessageId}.", record.MessageId);
            return false;
        }

        context.Logger.LogInformation(
            "[NOTIFY] Payment {PaymentId} received and pending: {Amount} {Currency}.",
            payment.PaymentId, payment.Amount, payment.Currency);
        return true;
    }

    private static bool HandleDecided(SQSEvent.SQSMessage record, ILambdaContext context)
    {
        var decision = SnsMessageUnwrapper.Unwrap<PaymentDecisionEvent>(record);
        if (decision is null)
        {
            context.Logger.LogWarning("Could not deserialise PaymentDecidedEvent from {MessageId}.", record.MessageId);
            return false;
        }

        if (decision.Status == "Authorized")
            context.Logger.LogInformation(
                "[NOTIFY] Payment {PaymentId} was authorized — notifying customer.", decision.PaymentId);
        else
            context.Logger.LogInformation(
                "[NOTIFY] Payment {PaymentId} was declined ({Reason}) — notifying customer.",
                decision.PaymentId, decision.Reason ?? "no reason given");
        return true;
    }
}
