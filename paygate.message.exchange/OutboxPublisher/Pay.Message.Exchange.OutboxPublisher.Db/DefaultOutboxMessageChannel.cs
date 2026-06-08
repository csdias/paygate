using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public class DefaultOutboxMessageChannel : IOutboxMessageChannel
{
    private readonly IOutbox _outbox;
    private readonly ILogger<DefaultOutboxMessageChannel> _logger;
    private readonly TimeSpan _sleepDelay;

    public DefaultOutboxMessageChannel(IOutbox outbox, IOptions<DatabaseOptions> options,
        ILogger<DefaultOutboxMessageChannel> logger)
    {
        _outbox = outbox;
        _logger = logger;
        _sleepDelay = options.Value.SleepDelay;
    }

    public async Task ReserveAndFetchUnpublishedMessagesAsync(ChannelWriter<OutboxMessage> writer,
        CancellationToken cancellation)
    {
        try
        {
            var runId = Guid.NewGuid();
            while (!cancellation.IsCancellationRequested)
            {
                IReadOnlyCollection<OutboxMessage> messages;
                do
                {
                    messages = await _outbox.ReserveAndFetchUnpublishedMessagesAsync(runId);
                    if (messages.Count == 0) break;
                    foreach (var message in messages)
                        await writer.WriteAsync(message);
                } while (messages.Count > 0);

                await Task.Delay(_sleepDelay, cancellation);
            }

            writer.Complete();
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Cancelling outbox gracefully");
            writer.Complete();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Caught exception while retrieving messages, aborting");
            writer.Complete(e);
        }
    }

    public async Task MarkMessagesAsPublishedAsync(ChannelReader<OutboxMessage> reader)
    {
        await foreach (var message in reader.ReadAllAsync())
        {
            _logger.LogTrace("Marking message id: {MessageId} as done", message.MessageId);
            await _outbox.MarkMessagesAsPublishedAsync(message);
        }
    }

    public async Task MarkMessagesAsFailedAsync(ChannelReader<OutboxMessage> reader)
    {
        await foreach (var message in reader.ReadAllAsync())
        {
            _logger.LogWarning("Failed to deliver message id: {MessageId}", message.MessageId);
            await _outbox.MarkMessagesAsFailedAsync(message);
        }
    }
}
