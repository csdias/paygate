namespace Pay.Message.Exchange.OutboxPublisher.Sns.Entities;

public class Metadata
{
    public int MessageType { get; set; }
    public Guid MessageId { get; set; }
    public string MessageName { get; set; }
    public string MessageVersion { get; set; }
    public string ContextId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Topic { get; set; }
    public Guid? ProcessedBy { get; set; }
    public Guid? PredecessorId { get; set; }
    public string TraceParent { get; set; }
    public string TraceState { get; set; }
}
