using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient;

public interface IOutboxMessageRepository
{
    Task<Guid> CreateOutboxMessage(OutboxMessage outboxMessage);
    Task<OutboxMessage> GetById(Guid id);
}
