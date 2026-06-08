namespace Paygate.Domain;

public class Payment
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = default!;
    public Guid CustomerId { get; init; }   // cardholder paying
    public Guid MerchantId { get; init; }   // business being paid
    public Guid? CardId { get; init; }       // which of the customer's cards was used
    public string Processor { get; init; } = "Omni Card";
    public string Status { get; set; } = PaymentStatus.Pending;
    public string? DeclineReason { get; set; }
    public string? Reference { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
