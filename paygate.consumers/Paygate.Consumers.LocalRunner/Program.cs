using System.Diagnostics;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paygate.Consumers.LocalRunner;

// W3C ids so the trace context restored from each message's traceparent lines up
// with the API and OutboxPublisher.
Activity.DefaultIdFormat = ActivityIdFormat.W3C;

var endpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "http://localhost:4566";
var region   = Environment.GetEnvironmentVariable("AWS_REGION") ?? "eu-west-1";
var notificationQueue = Environment.GetEnvironmentVariable("QUEUE_NOTIFICATION") ?? "paygate-local-notification";
var auditQueue        = Environment.GetEnvironmentVariable("QUEUE_AUDIT") ?? "paygate-local-audit";

var sqs = new AmazonSQSClient(
    new BasicAWSCredentials("test", "test"),
    new AmazonSQSConfig { ServiceURL = endpoint, AuthenticationRegion = region });

// Map each queue to the Lambda handler it triggers in AWS. The notification
// handler is synchronous; wrap it so both share one signature.
var notificationFn = new Paygate.Notification.Function();
var auditFn        = new Paygate.Audit.Function();

var consumers = new (string Queue, string Name, Func<SQSEvent, ILambdaContext, Task<SQSBatchResponse>> Handler)[]
{
    (notificationQueue, "Paygate.Notification",
        (e, ctx) => Task.FromResult(notificationFn.Handler(e, ctx))),
    (auditQueue, "Paygate.Audit",
        (e, ctx) => auditFn.Handler(e, ctx)),
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// Forward consumer logs to the Signals panel (no-op unless TELEMETRY_INGEST_URL is set).
TelemetryForwarder.Start(cts.Token);

Console.WriteLine($"Paygate consumer poller — endpoint {endpoint}, region {region}");
Console.WriteLine($"Polling: {notificationQueue}, {auditQueue}. Ctrl+C to stop.");

var loops = consumers.Select(c => PollQueueAsync(c.Queue, c.Name, c.Handler, cts.Token));
try { await Task.WhenAll(loops); }
catch (OperationCanceledException) { /* shutting down */ }

Console.WriteLine("Stopped.");
return;

async Task PollQueueAsync(string queueName, string functionName,
    Func<SQSEvent, ILambdaContext, Task<SQSBatchResponse>> handler, CancellationToken ct)
{
    var queueUrl = (await sqs.GetQueueUrlAsync(queueName, ct)).QueueUrl;
    var context = new ConsoleLambdaContext(functionName);

    while (!ct.IsCancellationRequested)
    {
        ReceiveMessageResponse received;
        try
        {
            received = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 20,     // long poll
                VisibilityTimeout = 30
            }, ct);
        }
        catch (OperationCanceledException) { break; }
        catch (Exception ex)
        {
            Console.WriteLine($"[{functionName}] receive error: {ex.Message}");
            await Task.Delay(1000, ct);
            continue;
        }

        var messages = received.Messages ?? new List<Message>();
        if (messages.Count == 0) continue;

        // Mirror the AWS SQS→Lambda event-source mapping: hand the batch to the
        // handler, then delete only the records it did NOT report as failures
        // (partial batch failure). Failed records become visible again after the
        // visibility timeout and are retried — exactly as in AWS.
        var sqsEvent = new SQSEvent
        {
            Records = messages.Select(m => new SQSEvent.SQSMessage
            {
                MessageId = m.MessageId,
                Body = m.Body,
                ReceiptHandle = m.ReceiptHandle
            }).ToList()
        };

        SQSBatchResponse response;
        try
        {
            response = await handler(sqsEvent, context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{functionName}] handler threw, batch will retry: {ex.Message}");
            continue; // delete nothing → whole batch redelivers
        }

        var failed = response.BatchItemFailures?.Select(f => f.ItemIdentifier).ToHashSet()
                     ?? new HashSet<string>();

        foreach (var m in messages.Where(m => !failed.Contains(m.MessageId)))
            await sqs.DeleteMessageAsync(queueUrl, m.ReceiptHandle, ct);
    }
}
