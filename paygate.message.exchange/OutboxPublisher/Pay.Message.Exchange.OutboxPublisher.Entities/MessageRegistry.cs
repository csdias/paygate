namespace Pay.Message.Exchange.OutboxPublisher.Entities;

public class MessageRegistry
{
    public Guid MessageRegistryId { get; set; }
    public string MessageName { get; set; }
    public string MessageVersion { get; set; }
    public int MessageTypeId { get; set; }
    public string Topic { get; set; }
    public int RetryLimit { get; set; } = 4;
    public int RetryBackoffInSeconds { get; set; } = 60;
    public MessageType MessageType { get; set; }
}
