namespace Paygate.Consumers.Common;

public record PaymentInitiatedEvent(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    Guid CustomerId,
    Guid MerchantId,
    string? Reference,
    DateTimeOffset CreatedAt);
