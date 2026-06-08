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

    [Fact]
    public async Task PostPayment_ValidRequest_Returns201WithLocation()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/payments",
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
    public async Task PostPayment_ValidRequest_WritesOutboxRow()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(75.50m, "USD"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        var outboxCount = await _fixture.CountOutboxRowsAsync(body!.PaymentId.ToString());
        outboxCount.Should().Be(1);
    }

    [Fact]
    public async Task ApprovePayment_PendingPayment_AuthorizesAndWritesDecisionEvent()
    {
        var created = await (await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(40m, "EUR")))
            .Content.ReadFromJsonAsync<PaymentResponse>();

        var approve = await _fixture.Client.PostAsync($"/payments/{created!.PaymentId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var decided = await approve.Content.ReadFromJsonAsync<PaymentResponse>();
        decided!.Status.Should().Be("Authorized");

        // Two outbox rows now exist for this payment: the initiated event + the decision event.
        var outboxCount = await _fixture.CountOutboxRowsAsync(created.PaymentId.ToString());
        outboxCount.Should().Be(2);
    }

    [Fact]
    public async Task RejectPayment_PendingPayment_DeclinesWithReason()
    {
        var created = await (await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(40m, "EUR")))
            .Content.ReadFromJsonAsync<PaymentResponse>();

        var reject = await _fixture.Client.PostAsJsonAsync(
            $"/payments/{created!.PaymentId}/reject", new RejectPaymentRequest("Insufficient funds"));
        reject.StatusCode.Should().Be(HttpStatusCode.OK);

        var decided = await reject.Content.ReadFromJsonAsync<PaymentResponse>();
        decided!.Status.Should().Be("Declined");
        decided.DeclineReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public async Task ApprovePayment_AlreadyDecided_ReturnsConflict()
    {
        var created = await (await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(40m, "EUR")))
            .Content.ReadFromJsonAsync<PaymentResponse>();

        await _fixture.Client.PostAsync($"/payments/{created!.PaymentId}/approve", null);
        var second = await _fixture.Client.PostAsync($"/payments/{created.PaymentId}/approve", null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetPayment_ExistingId_Returns200()
    {
        var createResponse = await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(100m, "GBP"));
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        var getResponse = await _fixture.Client.GetAsync($"/payments/{created!.PaymentId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        payment!.PaymentId.Should().Be(created.PaymentId);
        payment.Amount.Should().Be(100m);
        payment.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task GetPayment_UnknownId_Returns404()
    {
        var response = await _fixture.Client.GetAsync($"/payments/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostPayment_ZeroAmount_Returns400()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(0m, "EUR"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostPayment_InvalidCurrency_Returns400()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(10m, "EU"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListPayments_ReturnsPagedResult()
    {
        for (var i = 0; i < 3; i++)
            await _fixture.Client.PostAsJsonAsync("/payments", NewRequest(10m + i, "EUR"));

        var response = await _fixture.Client.GetAsync("/payments?limit=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PaymentResponse>>();
        page!.Items.Should().HaveCount(2);
    }
}
