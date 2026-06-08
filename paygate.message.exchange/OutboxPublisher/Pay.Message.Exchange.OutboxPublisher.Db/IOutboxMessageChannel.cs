using System.Threading.Channels;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public interface IOutboxMessageChannel
{
    Task ReserveAndFetchUnpublishedMessagesAsync(ChannelWriter<OutboxMessage> writer, CancellationToken token);
    Task MarkMessagesAsPublishedAsync(ChannelReader<OutboxMessage> reader);
    Task MarkMessagesAsFailedAsync(ChannelReader<OutboxMessage> reader);
}
