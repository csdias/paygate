namespace Pay.Message.Exchange.OutboxPublisher.Services;

public interface IMessageProcessor
{
    Task ProcessMessagesAsync(CancellationToken cancellationToken = default);
}
