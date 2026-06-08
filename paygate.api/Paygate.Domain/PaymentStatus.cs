namespace Paygate.Domain;

public static class PaymentStatus
{
    public const string Pending    = "Pending";     // awaiting an authorization decision
    public const string Authorized = "Authorized";  // acquirer authorized the charge
    public const string Declined   = "Declined";    // acquirer declined the charge
}
