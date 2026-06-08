using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Paygate.Notification;

var serialiser = new DefaultLambdaJsonSerializer();
var function = new Function();

await LambdaBootstrapBuilder
    .Create<SQSEvent, SQSBatchResponse>(function.Handler, serialiser)
    .Build()
    .RunAsync();
