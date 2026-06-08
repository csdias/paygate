namespace Paygate.Api.Models;

public record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    Guid CustomerId,   // cardholder paying
    Guid MerchantId,   // business being paid
    Guid CardId,       // which of the customer's cards to charge
    string? Reference);
