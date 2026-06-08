using Paygate.Domain;

namespace Paygate.Api.Endpoints;

public static class CustomerEndpoints
{
    // A small fixed set of demo cardholders to choose from in the UI. Stable ids so
    // created payments reference consistent customers/cards across restarts.
    // Each customer owns two cards, the first marked main.
    private static readonly Customer[] Customers =
    [
        Customer("11111111-1111-1111-1111-111111111111", "Alice Martin",   "Visa",       "4242", "Mastercard", "5301"),
        Customer("22222222-2222-2222-2222-222222222222", "Liam Chen",       "Mastercard", "8210", "Visa",       "1881"),
        Customer("33333333-3333-3333-3333-333333333333", "Sofia Rossi",     "Amex",       "0005", "Visa",       "3737"),
        Customer("44444444-4444-4444-4444-444444444444", "Noah Patel",      "Visa",       "9111", "Amex",       "1007"),
        Customer("55555555-5555-5555-5555-555555555555", "Emma Dubois",     "Mastercard", "6440", "Visa",       "2920"),
        Customer("66666666-6666-6666-6666-666666666666", "Lucas Silva",     "Visa",       "7010", "Mastercard", "4455"),
    ];

    // Builds a customer with two cards (the first is main). Card ids are derived from the
    // customer id so they stay stable across restarts without hand-writing every guid.
    private static Customer Customer(string customerId, string name,
        string brand1, string last4_1, string brand2, string last4_2)
    {
        var cid = Guid.Parse(customerId);
        return new Customer(cid, name,
        [
            new Card(Derive(cid, 1), brand1, last4_1, IsMain: true),
            new Card(Derive(cid, 2), brand2, last4_2, IsMain: false),
        ]);
    }

    private static Guid Derive(Guid customerId, byte n)
    {
        var b = customerId.ToByteArray();
        b[^1] = n;   // vary the last byte so each card id is distinct but deterministic
        return new Guid(b);
    }

    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/customers", () => Results.Ok(Customers))
            .WithName("ListCustomers")
            .WithTags("Customers");

        return app;
    }
}
