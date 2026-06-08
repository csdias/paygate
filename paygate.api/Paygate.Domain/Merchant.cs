namespace Paygate.Domain;

/// <summary>
/// A merchant — the gateway's client, the business being paid. Unlike a customer it
/// holds no card; it accepts payments into a settlement account. In a real gateway the
/// merchant is the authenticated caller (identified by an API key); here it's a small
/// fixed demo set (see MerchantEndpoints) so created payments reference stable merchants.
/// </summary>
public record Merchant(Guid Id, string Name, string Category, string SettlementCurrency);
