using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public interface IOutbox
{
    Task<IReadOnlyCollection<OutboxMessage>> ReserveAndFetchUnpublishedMessagesAsync(Guid runId);
    Task MarkMessagesAsPublishedAsync(OutboxMessage message);
    Task MarkMessagesAsFailedAsync(OutboxMessage message);
}
