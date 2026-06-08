using System.Text.Json;
using Paygate.Domain;
using Pay.Message.Exchange.OutboxClient;

namespace Paygate.Data;

public class PaymentService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITransactionalOutboxWriter _outboxWriter;
    private readonly IMessageRegistryRepository _registryRepository;

    public PaymentService(
        IDbConnectionFactory connectionFactory,
        IPaymentRepository paymentRepository,
        ITransactionalOutboxWriter outboxWriter,
        IMessageRegistryRepository registryRepository)
    {
        _connectionFactory = connectionFactory;
        _paymentRepository = paymentRepository;
        _outboxWriter = outboxWriter;
        _registryRepository = registryRepository;
    }

    public async Task<Payment> CreateAsync(decimal amount, string currency, Guid customerId, Guid merchantId,
        Guid cardId, string? reference = null)
    {
        var registry = await ResolveRegistryAsync("PaymentInitiatedEvent");

        using var connection = _connectionFactory.Create();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            Amount = amount,
            Currency = currency,
            CustomerId = customerId,
            MerchantId = merchantId,
            CardId = cardId,
            Processor = "Omni Card",
            Status = PaymentStatus.Pending,
            Reference = reference,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var created = await _paymentRepository.InsertAsync(payment, transaction);

        var messageBody = JsonSerializer.Serialize(new
        {
            created.PaymentId,
            created.Amount,
            created.Currency,
            created.CustomerId,
            created.MerchantId,
            created.Reference,
            created.CreatedAt
        });

        await _outboxWriter.WriteAsync(registry.MessageRegistryId, created.PaymentId.ToString(),
            messageBody, transaction);

        transaction.Commit();
        return created;
    }

    public Task<Payment?> GetByIdAsync(Guid paymentId)
        => _paymentRepository.GetByIdAsync(paymentId);

    public Task<IReadOnlyList<Payment>> GetPageAsync(Guid? afterId, int limit = 20, string? status = null)
        => _paymentRepository.GetPageAsync(afterId, limit, status);

    /// <summary>
    /// Acquirer decision (driven from the admin). Transitions a Pending payment to Authorized/Declined AND writes a
    /// PaymentDecidedEvent to the outbox in the same transaction (so the decision reaches the
    /// notification pipeline reliably). Returns null if the payment isn't Pending any more.
    /// </summary>
    public async Task<Payment?> DecideAsync(Guid paymentId, bool approved, string? reason)
    {
        var status = approved ? PaymentStatus.Authorized : PaymentStatus.Declined;
        var registry = await ResolveRegistryAsync("PaymentDecidedEvent");

        using var connection = _connectionFactory.Create();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var updated = await _paymentRepository.UpdateDecisionAsync(
            paymentId, status, approved ? null : reason, transaction);

        if (updated is null)
        {
            transaction.Rollback();
            return null; // not found, or already decided
        }

        var messageBody = JsonSerializer.Serialize(new
        {
            updated.PaymentId,
            Status = status,
            Reason = approved ? null : reason,
            DecidedAt = updated.UpdatedAt
        });

        await _outboxWriter.WriteAsync(registry.MessageRegistryId, updated.PaymentId.ToString(),
            messageBody, transaction);

        transaction.Commit();
        return updated;
    }

    private async Task<Pay.Message.Exchange.OutboxPublisher.Entities.MessageRegistry> ResolveRegistryAsync(string messageName)
    {
        var all = await _registryRepository.GetAllAsync();
        var registry = all.FirstOrDefault(r => r.MessageName == messageName);
        if (registry is null)
            throw new InvalidOperationException(
                $"No message registry entry found for '{messageName}'. Ensure the outbox schema has been seeded.");
        return registry;
    }
}
