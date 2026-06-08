namespace Paygate.Domain;

/// <summary>
/// A cardholder — the person paying. In a real gateway these come from a customer/
/// vault service keyed off the presented card; here it's a small fixed demo set
/// (see CustomerEndpoints). Each customer owns a few cards, one marked main.
/// </summary>
public record Customer(Guid Id, string Name, IReadOnlyList<Card> Cards);

/// <summary>A payment card belonging to a customer. Demo data — no real PAN, just a brand and last four.</summary>
public record Card(Guid Id, string Brand, string Last4, bool IsMain);
