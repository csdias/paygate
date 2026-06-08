namespace Paygate.Consumers.Common;

/// <summary>
/// Published by the backoffice approve/reject action. Status is "Authorized" or "Declined";
/// Reason is set only on a decline.
/// </summary>
public record PaymentDecisionEvent(
    Guid PaymentId,
    string Status,
    string? Reason,
    DateTimeOffset DecidedAt);
