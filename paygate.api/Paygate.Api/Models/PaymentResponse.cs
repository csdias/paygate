namespace Paygate.Api.Models;

public record PaymentResponse(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    Guid CustomerId,
    Guid MerchantId,
    Guid? CardId,
    string Processor,
    string Status,
    string? DeclineReason,
    string? Reference,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
