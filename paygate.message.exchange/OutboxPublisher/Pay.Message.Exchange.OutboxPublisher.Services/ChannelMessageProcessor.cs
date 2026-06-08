using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;
using Pay.Message.Exchange.OutboxPublisher.Sns;

namespace Pay.Message.Exchange.OutboxPublisher.Services;

public class ChannelMessageProcessor : IMessageProcessor
{
    private readonly Channel<OutboxMessage> _reservedChannel;
    private readonly Channel<OutboxMessage> _successChannel;
    private readonly Channel<OutboxMessage> _failedChannel;

    private readonly IMessagePublisher _messagePublisher;
    private readonly IOutboxMessageChannel _outboxMessageChannel;
    private readonly ILogger<ChannelMessageProcessor> _logger;

    public ChannelMessageProcessor(IMessagePublisher messagePublisher,
        IOutboxMessageChannel outboxMessageChannel, ILogger<ChannelMessageProcessor> logger)
    {
        _messagePublisher = messagePublisher;
        _outboxMessageChannel = outboxMessageChannel;
        _logger = logger;

        _reservedChannel = Channel.CreateBounded<OutboxMessage>(100);
        _successChannel = Channel.CreateBounded<OutboxMessage>(100);
        _failedChannel = Channel.CreateBounded<OutboxMessage>(100);
    }

    public async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        var processInternal = ProcessMessageInternal(
            _reservedChannel.Reader, _successChannel.Writer, _failedChannel.Writer, cancellationToken);
        var handleFailed = _outboxMessageChannel.MarkMessagesAsFailedAsync(_failedChannel.Reader);
        var handleSuccess = _outboxMessageChannel.MarkMessagesAsPublishedAsync(_successChannel.Reader);
        var readMessages = _outboxMessageChannel.ReserveAndFetchUnpublishedMessagesAsync(
            _reservedChannel.Writer, cancellationToken);

        await Task.WhenAll(processInternal, handleFailed, handleSuccess, readMessages);
    }

    private async Task ProcessMessageInternal(ChannelReader<OutboxMessage> messageReader,
        ChannelWriter<OutboxMessage> successWriter, ChannelWriter<OutboxMessage> failureWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in messageReader.ReadAllAsync(cancellationToken))
            {
                using var activity = new Activity("outbox.publish");
                if (!string.IsNullOrEmpty(message.TraceParent))
                    activity.SetParentId(message.TraceParent);
                activity.Start();

                _logger.LogInformation("Received message id: {MessageId}, publishing...", message.MessageId);
                bool success;
                try
                {
                    success = await _messagePublisher.PublishMessages(message);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Caught exception while publishing message, marking as failure");
                    success = false;
                }

                if (success)
                {
                    _logger.LogInformation("Published message id: {MessageId}, marking as published.", message.MessageId);
                    await successWriter.WriteAsync(message, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Failed message id {MessageId}, marking as failed.", message.MessageId);
                    await failureWriter.WriteAsync(message, cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Failure publishing messages");
        }
        finally
        {
            _logger.LogInformation("Finishing publishing messages");
            successWriter.Complete();
            failureWriter.Complete();
        }
    }
}
