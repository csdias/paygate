using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using FluentAssertions;
using Paygate.Consumers.Common;

namespace Paygate.Consumers.Tests;

public class SnsMessageUnwrapperTests
{
    private static SQSEvent.SQSMessage BuildSqsMessage(PaymentInitiatedEvent payload)
    {
        var innerJson = JsonSerializer.Serialize(payload);
        var envelope = new SnsEnvelope(
            Type: "Notification",
            MessageId: Guid.NewGuid().ToString(),
            TopicArn: "arn:aws:sns:eu-west-1:000000000000:payment-events",
            Message: innerJson,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));

        return new SQSEvent.SQSMessage { Body = JsonSerializer.Serialize(envelope) };
    }

    [Fact]
    public void Unwrap_ValidEnvelope_ReturnsDeserializedPayload()
    {
        var expected = new PaymentInitiatedEvent(
            PaymentId: Guid.NewGuid(),
            Amount: 99.50m,
            Currency: "EUR",
            CustomerId: Guid.NewGuid(),
            MerchantId: Guid.NewGuid(),
            Reference: "INV-007",
            CreatedAt: DateTimeOffset.UtcNow);

        var message = BuildSqsMessage(expected);

        var result = SnsMessageUnwrapper.Unwrap<PaymentInitiatedEvent>(message);

        result.Should().NotBeNull();
        result!.PaymentId.Should().Be(expected.PaymentId);
        result.Amount.Should().Be(expected.Amount);
        result.Currency.Should().Be(expected.Currency);
        result.Reference.Should().Be(expected.Reference);
    }

    [Fact]
    public void Unwrap_MalformedBody_ReturnsNull()
    {
        var message = new SQSEvent.SQSMessage { Body = "not-valid-json" };

        var result = SnsMessageUnwrapper.Unwrap<PaymentInitiatedEvent>(message);

        result.Should().BeNull();
    }

    [Fact]
    public void Unwrap_ValidEnvelopeButWrongInnerType_ReturnsDefault()
    {
        var message = new SQSEvent.SQSMessage
        {
            Body = JsonSerializer.Serialize(new SnsEnvelope(
                "Notification", Guid.NewGuid().ToString(),
                "arn:aws:sns:eu-west-1:000:topic", "{}", DateTimeOffset.UtcNow.ToString("O")))
        };

        var result = SnsMessageUnwrapper.Unwrap<PaymentInitiatedEvent>(message);

        // Empty JSON object deserialises to a record with default values — not null
        result.Should().NotBeNull();
        result!.PaymentId.Should().Be(Guid.Empty);
    }
}
