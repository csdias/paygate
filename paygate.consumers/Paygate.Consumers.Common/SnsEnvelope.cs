using System.Text.Json.Serialization;

namespace Paygate.Consumers.Common;

/// <summary>
/// Wraps the SNS notification that SQS receives when raw_message_delivery = false.
/// The actual event payload lives inside the "Message" string field as serialised JSON.
/// SNS message attributes (including traceparent) live in MessageAttributes.
/// </summary>
public record SnsEnvelope(
    [property: JsonPropertyName("Type")]              string Type,
    [property: JsonPropertyName("MessageId")]         string MessageId,
    [property: JsonPropertyName("TopicArn")]          string TopicArn,
    [property: JsonPropertyName("Message")]           string Message,
    [property: JsonPropertyName("Timestamp")]         string Timestamp,
    [property: JsonPropertyName("MessageAttributes")] Dictionary<string, SnsMessageAttribute>? MessageAttributes = null);

/// <summary>
/// An individual SNS message attribute as it appears in the notification envelope.
/// </summary>
public record SnsMessageAttribute(
    [property: JsonPropertyName("Type")]  string Type,
    [property: JsonPropertyName("Value")] string Value);
