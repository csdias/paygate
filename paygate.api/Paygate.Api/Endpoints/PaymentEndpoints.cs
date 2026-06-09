using System.Security.Claims;
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
            .RequireAuthorization("CanCreatePayment")
            .WithName("CreatePayment")
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetPaymentById)
            .RequireAuthorization("CanReadPayment")
            .WithName("GetPaymentById")
            .Produces<PaymentResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListPayments)
            .RequireAuthorization("CanReadPayment")
            .WithName("ListPayments")
            .Produces<PagedResponse<PaymentResponse>>();

        group.MapPost("/{id:guid}/approve", ApprovePayment)
            .RequireAuthorization("CanDecidePayment")
            .WithName("ApprovePayment")
            .Produces<PaymentResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", RejectPayment)
            .RequireAuthorization("CanDecidePayment")
            .WithName("RejectPayment")
            .Produces<PaymentResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ApprovePayment(
        Guid id, ClaimsPrincipal user, PaymentService service, ILogger<PaymentService> logger)
    {
        var outcome = await service.DecideAsync(id, approved: true, reason: null, ActorId(user));
        if (DecisionError(outcome, id, logger) is { } error)
            return error;

        logger.LogInformation("Payment {PaymentId} authorized by {Approver}.", id, ActorId(user));
        return Results.Ok(ToResponse(outcome.Payment!));
    }

    private static async Task<IResult> RejectPayment(
        Guid id, RejectPaymentRequest? body, ClaimsPrincipal user, PaymentService service, ILogger<PaymentService> logger)
    {
        var outcome = await service.DecideAsync(id, approved: false, reason: body?.Reason, ActorId(user));
        if (DecisionError(outcome, id, logger) is { } error)
            return error;

        logger.LogInformation("Payment {PaymentId} declined by {Approver}: {Reason}.", id, ActorId(user), body?.Reason ?? "n/a");
        return Results.Ok(ToResponse(outcome.Payment!));
    }

    // The token's `sub` is the actor id used for created_by / maker-checker comparison.
    private static string ActorId(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? "unknown";

    // Maps the non-success decision outcomes to the right status code; null means "no error".
    private static IResult? DecisionError(DecisionOutcome outcome, Guid id, ILogger logger) => outcome.Result switch
    {
        DecisionResult.Decided => null,
        DecisionResult.SelfApprovalRejected => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Maker-checker violation",
            detail: "You cannot approve or reject a payment you created."),
        _ => Results.Conflict("Payment not found or no longer pending."),
    };

    private static async Task<IResult> CreatePayment(
        CreatePaymentRequest request,
        ClaimsPrincipal user,
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
            request.CustomerId, request.MerchantId, request.CardId, ActorId(user), request.Reference);

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
