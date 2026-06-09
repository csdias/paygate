using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Paygate.Api.IntegrationTests.Fixtures;
using Paygate.Api.Models;

namespace Paygate.Api.IntegrationTests.Tests;

[Collection(ApiIntegrationTestCollectionFixture.Name)]
public class PaymentEndpointTests
{
    private readonly ApiIntegrationTestFixture _fixture;

    public PaymentEndpointTests(ApiIntegrationTestFixture fixture)
        => _fixture = fixture;

    private static CreatePaymentRequest NewRequest(
        decimal amount = 150.00m, string currency = "EUR", string? reference = null) =>
        new(amount, currency, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reference);

    // Helper: a clerk creates a Pending payment and returns it (the common arrange step).
    private async Task<PaymentResponse> CreatePendingAsClerkAsync(CreatePaymentRequest? request = null)
    {
        var response = await _fixture.AsClerk().PostAsJsonAsync("/payments", request ?? NewRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PaymentResponse>())!;
    }

    // ── Happy-path behavior (now exercised through the correct personas) ──────────────

    [Fact]
    public async Task PostPayment_AsClerk_Returns201WithLocation()
    {
        var response = await _fixture.AsClerk().PostAsJsonAsync("/payments",
            NewRequest(150.00m, "EUR", "INV-001"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        body!.Amount.Should().Be(150.00m);
        body.Currency.Should().Be("EUR");
        body.Status.Should().Be("Pending");
        body.Processor.Should().Be("Omni Card");
        body.Reference.Should().Be("INV-001");
    }

    [Fact]
    public async Task PostPayment_AsClerk_WritesOutboxRow()
    {
        var body = await CreatePendingAsClerkAsync(NewRequest(75.50m, "USD"));
        var outboxCount = await _fixture.CountOutboxRowsAsync(body.PaymentId.ToString());
        outboxCount.Should().Be(1);
    }

    [Fact]
    public async Task ApprovePayment_AsApprover_AuthorizesAndWritesDecisionEvent()
    {
        var created = await CreatePendingAsClerkAsync(NewRequest(40m, "EUR"));

        var approve = await _fixture.AsApprover().PostAsync($"/payments/{created.PaymentId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var decided = await approve.Content.ReadFromJsonAsync<PaymentResponse>();
        decided!.Status.Should().Be("Authorized");

        // Two outbox rows now exist for this payment: the initiated event + the decision event.
        var outboxCount = await _fixture.CountOutboxRowsAsync(created.PaymentId.ToString());
        outboxCount.Should().Be(2);
    }

    [Fact]
    public async Task RejectPayment_AsApprover_DeclinesWithReason()
    {
        var created = await CreatePendingAsClerkAsync(NewRequest(40m, "EUR"));

        var reject = await _fixture.AsApprover().PostAsJsonAsync(
            $"/payments/{created.PaymentId}/reject", new RejectPaymentRequest("Insufficient funds"));
        reject.StatusCode.Should().Be(HttpStatusCode.OK);

        var decided = await reject.Content.ReadFromJsonAsync<PaymentResponse>();
        decided!.Status.Should().Be("Declined");
        decided.DeclineReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public async Task ApprovePayment_AlreadyDecided_ReturnsConflict()
    {
        var created = await CreatePendingAsClerkAsync(NewRequest(40m, "EUR"));

        await _fixture.AsApprover().PostAsync($"/payments/{created.PaymentId}/approve", null);
        var second = await _fixture.AsApprover().PostAsync($"/payments/{created.PaymentId}/approve", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetPayment_AsAuditor_Returns200()
    {
        var created = await CreatePendingAsClerkAsync(NewRequest(100m, "GBP"));

        var getResponse = await _fixture.AsAuditor().GetAsync($"/payments/{created.PaymentId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        payment!.PaymentId.Should().Be(created.PaymentId);
        payment.Amount.Should().Be(100m);
        payment.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task GetPayment_UnknownId_Returns404()
    {
        var response = await _fixture.AsAuditor().GetAsync($"/payments/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostPayment_ZeroAmount_Returns400()
    {
        var response = await _fixture.AsClerk().PostAsJsonAsync("/payments", NewRequest(0m, "EUR"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostPayment_InvalidCurrency_Returns400()
    {
        var response = await _fixture.AsClerk().PostAsJsonAsync("/payments", NewRequest(10m, "EU"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListPayments_AsAuditor_ReturnsPagedResult()
    {
        for (var i = 0; i < 3; i++)
            await _fixture.AsClerk().PostAsJsonAsync("/payments", NewRequest(10m + i, "EUR"));

        var response = await _fixture.AsAuditor().GetAsync("/payments?limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PaymentResponse>>();
        page!.Items.Should().HaveCount(2);
    }

    // ── Authorization + maker-checker ─────────────────────────────────────────────────

    [Fact]
    public async Task PostPayment_Anonymous_Returns401()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/payments", NewRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApprovePayment_AsClerk_Returns403_WrongRoleAndScope()
    {
        var created = await CreatePendingAsClerkAsync(NewRequest(40m, "EUR"));

        var approve = await _fixture.AsClerk().PostAsync($"/payments/{created.PaymentId}/approve", null);

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostPayment_AsAuditor_Returns403_ReadOnly()
    {
        var response = await _fixture.AsAuditor().PostAsJsonAsync("/payments", NewRequest());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApprovePayment_BySameUserWhoCreated_Returns403_MakerChecker()
    {
        // A single principal with both rights creates and then tries to approve its own payment.
        var dual = _fixture.AsMakerChecker("dual-user");
        var created = (await (await dual.PostAsJsonAsync("/payments", NewRequest(40m, "EUR")))
            .Content.ReadFromJsonAsync<PaymentResponse>())!;

        var approve = await dual.PostAsync($"/payments/{created.PaymentId}/approve", null);

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
