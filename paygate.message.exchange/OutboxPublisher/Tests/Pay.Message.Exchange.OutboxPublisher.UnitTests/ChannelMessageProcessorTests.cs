using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;
using Pay.Message.Exchange.OutboxPublisher.Services;
using Pay.Message.Exchange.OutboxPublisher.Sns;

namespace Pay.Message.Exchange.OutboxPublisher.UnitTests;

public class ChannelMessageProcessorTests
{
    private readonly Mock<IMessagePublisher> _publisher = new();
    private readonly Mock<IOutboxMessageChannel> _channel = new();
    private readonly Mock<ILogger<ChannelMessageProcessor>> _logger = new();

    private ChannelMessageProcessor CreateSut() =>
        new(_publisher.Object, _channel.Object, _logger.Object);

    [Fact]
    public async Task ProcessMessagesAsync_MessagePublishedSuccessfully_WritesToSuccessChannel()
    {
        var message = BuildMessage();
        var publishedMessages = new List<OutboxMessage>();
        var failedMessages = new List<OutboxMessage>();

        _channel.Setup(c => c.ReserveAndFetchUnpublishedMessagesAsync(
                It.IsAny<System.Threading.Channels.ChannelWriter<OutboxMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (System.Threading.Channels.ChannelWriter<OutboxMessage> writer, CancellationToken _) =>
            {
                await writer.WriteAsync(message);
                writer.Complete();
            });

        _channel.Setup(c => c.MarkMessagesAsPublishedAsync(
                It.IsAny<System.Threading.Channels.ChannelReader<OutboxMessage>>()))
            .Returns(async (System.Threading.Channels.ChannelReader<OutboxMessage> reader) =>
            {
                await foreach (var m in reader.ReadAllAsync())
                    publishedMessages.Add(m);
            });

        _channel.Setup(c => c.MarkMessagesAsFailedAsync(
                It.IsAny<System.Threading.Channels.ChannelReader<OutboxMessage>>()))
            .Returns(async (System.Threading.Channels.ChannelReader<OutboxMessage> reader) =>
            {
                await foreach (var m in reader.ReadAllAsync())
                    failedMessages.Add(m);
            });

        _publisher.Setup(p => p.PublishMessages(message)).ReturnsAsync(true);

        var sut = CreateSut();
        await sut.ProcessMessagesAsync(CancellationToken.None);

        publishedMessages.Should().ContainSingle().Which.Should().Be(message);
        failedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessagesAsync_PublishFails_WritesToFailureChannel()
    {
        var message = BuildMessage();
        var publishedMessages = new List<OutboxMessage>();
        var failedMessages = new List<OutboxMessage>();

        _channel.Setup(c => c.ReserveAndFetchUnpublishedMessagesAsync(
                It.IsAny<System.Threading.Channels.ChannelWriter<OutboxMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (System.Threading.Channels.ChannelWriter<OutboxMessage> writer, CancellationToken _) =>
            {
                await writer.WriteAsync(message);
                writer.Complete();
            });

        _channel.Setup(c => c.MarkMessagesAsPublishedAsync(
                It.IsAny<System.Threading.Channels.ChannelReader<OutboxMessage>>()))
            .Returns(async (System.Threading.Channels.ChannelReader<OutboxMessage> reader) =>
            {
                await foreach (var m in reader.ReadAllAsync())
                    publishedMessages.Add(m);
            });

        _channel.Setup(c => c.MarkMessagesAsFailedAsync(
                It.IsAny<System.Threading.Channels.ChannelReader<OutboxMessage>>()))
            .Returns(async (System.Threading.Channels.ChannelReader<OutboxMessage> reader) =>
            {
                await foreach (var m in reader.ReadAllAsync())
                    failedMessages.Add(m);
            });

        _publisher.Setup(p => p.PublishMessages(message)).ReturnsAsync(false);

        var sut = CreateSut();
        await sut.ProcessMessagesAsync(CancellationToken.None);

        failedMessages.Should().ContainSingle().Which.Should().Be(message);
        publishedMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessagesAsync_PublishThrows_WritesToFailureChannel()
    {
        var message = BuildMessage();
        var failedMessages = new List<OutboxMessage>();

        _channel.Setup(c => c.ReserveAndFetchUnpublishedMessagesAsync(
                It.IsAny<System.Threading.Channels.ChannelWriter<OutboxMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (System.Threading.Channels.ChannelWriter<OutboxMessage> writer, CancellationToken _) =>
            {
                await writer.WriteAsync(message);
                writer.Complete();
            });

        _channel.Setup(c => c.MarkMessagesAsPublishedAsync(
                It.IsAny<System.Threading.Channels.ChannelReader<OutboxMessage>>()))
            .Returns(async (System.Threading.Channels.ChannelReader<OutboxMessage> reader) =>
            {
                await foreach (var _ in reader.ReadAllAsync()) { }
            });

        _channel.Setup(c => c.MarkMessagesAsFailedAsync(
                It.IsAny<System.Threading.Channels.ChannelReader<OutboxMessage>>()))
            .Returns(async (System.Threading.Channels.ChannelReader<OutboxMessage> reader) =>
            {
                await foreach (var m in reader.ReadAllAsync())
                    failedMessages.Add(m);
            });

        _publisher.Setup(p => p.PublishMessages(message)).ThrowsAsync(new Exception("SNS is down"));

        var sut = CreateSut();
        await sut.ProcessMessagesAsync(CancellationToken.None);

        failedMessages.Should().ContainSingle().Which.Should().Be(message);
    }

    private static OutboxMessage BuildMessage() => new()
    {
        MessageId = Guid.NewGuid(),
        MessageBody = "{}",
        ContextId = "ctx-1",
        OccurredAt = DateTimeOffset.UtcNow,
        LastUpdated = DateTimeOffset.UtcNow,
        MessageFilters = Array.Empty<MessageFilter>(),
        MessageRegistry = new MessageRegistry
        {
            MessageRegistryId = Guid.NewGuid(),
            MessageName = "TestEvent",
            MessageVersion = "1.0",
            Topic = "arn:aws:sns:eu-west-1:123456789:test-topic",
            RetryLimit = 3
        }
    };
}
