namespace Pay.Message.Exchange.OutboxPublisher.Sns.Entities;

public class SnsMessage
{
    public object Payload { get; set; }
    public Metadata Metadata { get; set; }
}
