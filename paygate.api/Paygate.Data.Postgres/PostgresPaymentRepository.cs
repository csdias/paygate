using System.Data;
using Dapper;
using Paygate.Domain;

namespace Paygate.Data.Postgres;

public class PostgresPaymentRepository : IPaymentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PostgresPaymentRepository(IDbConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<Payment> InsertAsync(Payment payment, IDbTransaction transaction)
    {
        const string sql = @"
INSERT INTO payment (payment_id, amount, currency, customer_id, merchant_id, card_id, processor, status, reference, created_at, updated_at)
VALUES (@PaymentId, @Amount, @Currency, @CustomerId, @MerchantId, @CardId, @Processor, @Status, @Reference, @CreatedAt, @UpdatedAt)
RETURNING *";

        return await transaction.Connection!.QuerySingleAsync<Payment>(sql, payment, transaction);
    }

    public async Task<Payment?> GetByIdAsync(Guid paymentId)
    {
        using var connection = _connectionFactory.Create();
        return await connection.QueryFirstOrDefaultAsync<Payment>(
            "SELECT * FROM payment WHERE payment_id = @PaymentId",
            new { PaymentId = paymentId });
    }

    public async Task<IReadOnlyList<Payment>> GetPageAsync(Guid? afterId, int limit, string? status = null)
    {
        using var connection = _connectionFactory.Create();

        // Optional status filter (e.g. the admin queue of Pending payments).
        var statusClause = status is null ? "" : " AND status = @Status";
        var sql = afterId.HasValue
            ? $@"SELECT * FROM payment
                WHERE created_at < (SELECT created_at FROM payment WHERE payment_id = @AfterId){statusClause}
                ORDER BY created_at DESC
                LIMIT @Limit"
            : $"SELECT * FROM payment WHERE 1=1{statusClause} ORDER BY created_at DESC LIMIT @Limit";

        var results = await connection.QueryAsync<Payment>(sql, new { AfterId = afterId, Limit = limit, Status = status });
        return results.ToList();
    }

    public async Task<Payment?> UpdateDecisionAsync(Guid paymentId, string status, string? declineReason,
        IDbTransaction transaction)
    {
        // Only a Pending payment can be decided — the status guard makes this idempotent
        // (a second approve/reject affects no rows and returns null).
        const string sql = @"
UPDATE payment
SET status = @Status, decline_reason = @DeclineReason, updated_at = NOW()
WHERE payment_id = @PaymentId AND status = 'Pending'
RETURNING *";

        return await transaction.Connection!.QueryFirstOrDefaultAsync<Payment>(
            sql, new { PaymentId = paymentId, Status = status, DeclineReason = declineReason }, transaction);
    }
}
