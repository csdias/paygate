namespace Paygate.Audit;

public record AuditRecord(
    Guid AuditId,
    Guid PaymentId,
    string EventType,
    string Payload,
    DateTimeOffset OccurredAt);
