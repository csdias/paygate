namespace Pay.Message.Exchange.OutboxPublisher.Entities;

public class OutboxMessage
{
    public Guid MessageId { get; set; }
    public Guid MessageRegistryId { get; set; }
    public string ContextId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string MessageBody { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public int RetryCount { get; set; }
    public Guid? ProcessedBy { get; set; }
    public string TraceParent { get; set; }
    public string TraceState { get; set; }
    public Guid? PredecessorId { get; set; }
    public ICollection<MessageFilter> MessageFilters { get; set; }
    public MessageRegistry MessageRegistry { get; set; }
}
