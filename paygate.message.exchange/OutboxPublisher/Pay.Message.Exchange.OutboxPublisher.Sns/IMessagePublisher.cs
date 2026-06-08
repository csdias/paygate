using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Sns;

public interface IMessagePublisher
{
    Task<bool> PublishMessages(OutboxMessage message);
}
