namespace Pay.Message.Exchange.OutboxPublisher.Entities;

public class MessageFilter
{
    public Guid MessageId { get; set; }
    public string FilterKey { get; set; }
    public string FilterValue { get; set; }
}
