using System.Text.Json;
using Amazon.Lambda.SQSEvents;

namespace Paygate.Consumers.Common;

public static class SnsMessageUnwrapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private record SnsMetadata(string? MessageName, string? TraceParent, string? TraceState);
    private record SnsMessageWrapper(JsonElement? Payload, SnsMetadata? Metadata);

    /// <summary>
    /// Returns the event name the outbox stamped into the message (e.g. "PaymentInitiatedEvent",
    /// "PaymentDecidedEvent") so a consumer can branch on it. Null if absent.
    /// </summary>
    public static string? ExtractMessageName(SQSEvent.SQSMessage sqsMessage)
    {
        var envelope = JsonSerializer.Deserialize<SnsEnvelope>(sqsMessage.Body, Options);
        if (envelope is null) return null;

        var wrapper = JsonSerializer.Deserialize<SnsMessageWrapper>(envelope.Message, Options);
        return wrapper?.Metadata?.MessageName;
    }

    /// <summary>
    /// Peels off the SNS notification envelope from an SQS message body and
    /// deserialises the inner "Message" JSON into <typeparamref name="T"/>.
    /// Returns null if the body cannot be parsed (caller should DLQ the record).
    /// </summary>
    public static T? Unwrap<T>(SQSEvent.SQSMessage sqsMessage)
    {
        var envelope = JsonSerializer.Deserialize<SnsEnvelope>(sqsMessage.Body, Options);
        if (envelope is null) return default;

        // The outbox publishes a wrapper: { "Payload": {event}, "Metadata": {...} }.
        // The real event lives under Payload — deserialise that.
        var wrapper = JsonSerializer.Deserialize<SnsMessageWrapper>(envelope.Message, Options);
        if (wrapper?.Payload is { } payload)
            return payload.Deserialize<T>(Options);

        // Fallback: a raw (unwrapped) event body.
        return JsonSerializer.Deserialize<T>(envelope.Message, Options);
    }

    /// <summary>
    /// Extracts the W3C traceparent from the Metadata section of the SNS message body.
    /// Returns null if the message was not published via the outbox or has no trace context.
    /// </summary>
    public static string? ExtractTraceParent(SQSEvent.SQSMessage sqsMessage)
    {
        var envelope = JsonSerializer.Deserialize<SnsEnvelope>(sqsMessage.Body, Options);
        if (envelope is null) return null;

        // Standard location: SNS message attribute on the notification envelope
        if (envelope.MessageAttributes?.TryGetValue("traceparent", out var attr) == true)
            return attr.Value;

        // Fallback: body Metadata field (messages published before this fix)
        var wrapper = JsonSerializer.Deserialize<SnsMessageWrapper>(envelope.Message, Options);
        return wrapper?.Metadata?.TraceParent;
    }
}
