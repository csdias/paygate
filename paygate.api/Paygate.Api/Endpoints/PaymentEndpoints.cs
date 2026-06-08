using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Paygate.Api.Models;
using Paygate.Data;
using Paygate.Domain;

namespace Paygate.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapPost("/", CreatePayment)
            .WithName("CreatePayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetPaymentById)
            .WithName("GetPaymentById")
            .Produces<PaymentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListPayments)
            .WithName("ListPayments")
            .Produces<PagedResponse<PaymentResponse>>();

        group.MapPost("/{id:guid}/approve", ApprovePayment)
            .WithName("ApprovePayment")
            .Produces<PaymentResponse>()
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", RejectPayment)
            .WithName("RejectPayment")
            .Produces<PaymentResponse>()
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ApprovePayment(Guid id, PaymentService service, ILogger<PaymentService> logger)
    {
        var payment = await service.DecideAsync(id, approved: true, reason: null);
        if (payment is null)
            return Results.Conflict("Payment not found or no longer pending.");

        logger.LogInformation("Payment {PaymentId} authorized.", id);
        return Results.Ok(ToResponse(payment));
    }

    private static async Task<IResult> RejectPayment(
        Guid id, RejectPaymentRequest? body, PaymentService service, ILogger<PaymentService> logger)
    {
        var payment = await service.DecideAsync(id, approved: false, reason: body?.Reason);
        if (payment is null)
            return Results.Conflict("Payment not found or no longer pending.");

        logger.LogInformation("Payment {PaymentId} declined: {Reason}.", id, body?.Reason ?? "n/a");
        return Results.Ok(ToResponse(payment));
    }

    private static async Task<IResult> CreatePayment(
        CreatePaymentRequest request,
        PaymentService service,
        ILogger<PaymentService> logger)
    {
        if (request.Amount <= 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(request.Amount), ["Amount must be greater than zero."] }
            });

        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { nameof(request.Currency), ["Currency must be a 3-character ISO code."] }
            });

        var payment = await service.CreateAsync(
            request.Amount, request.Currency.ToUpperInvariant(),
            request.CustomerId, request.MerchantId, request.CardId, request.Reference);

        // Emits on the request's Activity — the same trace id written to the outbox
        // traceparent, so this entry anchors the trace in the Signals panel.
        logger.LogInformation(
            "Payment {PaymentId} initiated: {Amount} {Currency}.",
            payment.PaymentId, payment.Amount, payment.Currency);

        return Results.CreatedAtRoute("GetPaymentById", new { id = payment.PaymentId }, ToResponse(payment));
    }

    private static async Task<IResult> GetPaymentById(Guid id, PaymentService service)
    {
        var payment = await service.GetByIdAsync(id);
        return payment is null ? Results.NotFound() : Results.Ok(ToResponse(payment));
    }

    private static async Task<IResult> ListPayments(
        PaymentService service,
        Guid? after = null,
        int limit = 20,
        string? status = null)
    {
        limit = Math.Clamp(limit, 1, 100);
        var items = await service.GetPageAsync(after, limit, status);
        var nextCursor = items.Count == limit ? items.Last().PaymentId : (Guid?)null;
        return Results.Ok(new PagedResponse<PaymentResponse>(items.Select(ToResponse).ToList(), nextCursor));
    }

    private static PaymentResponse ToResponse(Payment p) =>
        new(p.PaymentId, p.Amount, p.Currency, p.CustomerId, p.MerchantId,
            p.CardId, p.Processor, p.Status, p.DeclineReason, p.Reference, p.CreatedAt, p.UpdatedAt);
}
