using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using Paygate.Consumers.Common;
using Paygate.Notification;

namespace Paygate.Consumers.Tests;

public class NotificationFunctionTests
{
    private readonly Function _function = new();
    private readonly TestLambdaContext _context = new();

    private static SQSEvent BuildEvent(params PaymentInitiatedEvent[] payloads)
    {
        var records = payloads.Select(p =>
        {
            var envelope = new SnsEnvelope(
                "Notification", Guid.NewGuid().ToString(),
                "arn:aws:sns:eu-west-1:000:payment-events",
                JsonSerializer.Serialize(p),
                DateTimeOffset.UtcNow.ToString("O"));

            return new SQSEvent.SQSMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = JsonSerializer.Serialize(envelope)
            };
        }).ToList();

        return new SQSEvent { Records = records };
    }

    [Fact]
    public void Handler_ValidBatch_ReturnsNoFailures()
    {
        var evt = BuildEvent(
            new PaymentInitiatedEvent(Guid.NewGuid(), 100m, "EUR",
                Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow),
            new PaymentInitiatedEvent(Guid.NewGuid(), 50m, "GBP",
                Guid.NewGuid(), Guid.NewGuid(), "REF-1", DateTimeOffset.UtcNow));

        var response = _function.Handler(evt, _context);

        response.BatchItemFailures.Should().BeEmpty();
    }

    [Fact]
    public void Handler_OneInvalidRecord_ReportsOnlyThatFailure()
    {
        var goodPayment = new PaymentInitiatedEvent(Guid.NewGuid(), 75m, "USD",
            Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow);

        var badRecord = new SQSEvent.SQSMessage
        {
            MessageId = "bad-record-id",
            Body = "not-valid-json"
        };

        var goodEnvelope = new SnsEnvelope(
            "Notification", Guid.NewGuid().ToString(),
            "arn:aws:sns:eu-west-1:000:payment-events",
            JsonSerializer.Serialize(goodPayment),
            DateTimeOffset.UtcNow.ToString("O"));

        var goodRecord = new SQSEvent.SQSMessage
        {
            MessageId = "good-record-id",
            Body = JsonSerializer.Serialize(goodEnvelope)
        };

        var evt = new SQSEvent { Records = [badRecord, goodRecord] };

        var response = _function.Handler(evt, _context);

        response.BatchItemFailures.Should().ContainSingle()
            .Which.ItemIdentifier.Should().Be("bad-record-id");
    }

    [Fact]
    public void Handler_EmptyBatch_ReturnsNoFailures()
    {
        var evt = new SQSEvent { Records = [] };

        var response = _function.Handler(evt, _context);

        response.BatchItemFailures.Should().BeEmpty();
    }
}
