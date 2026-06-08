using Paygate.Domain;

namespace Paygate.Api.Endpoints;

public static class MerchantEndpoints
{
    // A small fixed set of demo merchants — the businesses being paid. Stable ids so
    // created payments reference consistent merchants across restarts. Unlike customers,
    // merchants hold no card; they accept payments into a settlement account.
    private static readonly Merchant[] Merchants =
    [
        new(Guid.Parse("a1111111-1111-1111-1111-111111111111"), "Acme Store",        "Retail",        "GBP"),
        new(Guid.Parse("a2222222-2222-2222-2222-222222222222"), "Globex Online",     "E-commerce",    "EUR"),
        new(Guid.Parse("a3333333-3333-3333-3333-333333333333"), "Initech Cloud",     "Software",      "USD"),
        new(Guid.Parse("a4444444-4444-4444-4444-444444444444"), "Umbrella Pharmacy", "Healthcare",    "GBP"),
        new(Guid.Parse("a5555555-5555-5555-5555-555555555555"), "Wonka Sweets",      "Food & Drink",  "EUR"),
        new(Guid.Parse("a6666666-6666-6666-6666-666666666666"), "Stark Travel",      "Travel",        "USD"),
    ];

    public static IEndpointRouteBuilder MapMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/merchants", () => Results.Ok(Merchants))
            .WithName("ListMerchants")
            .WithTags("Merchants");

        return app;
    }
}
