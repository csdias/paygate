using System.Data;

namespace Paygate.Domain;

public interface IPaymentRepository
{
    Task<Payment> InsertAsync(Payment payment, IDbTransaction transaction);
    Task<Payment?> GetByIdAsync(Guid paymentId);
    Task<IReadOnlyList<Payment>> GetPageAsync(Guid? afterId, int limit, string? status = null);

    /// <summary>
    /// Transitions a Pending payment to the given status (within the caller's transaction),
    /// returning the updated row — or null if it doesn't exist or is no longer Pending.
    /// </summary>
    Task<Payment?> UpdateDecisionAsync(Guid paymentId, string status, string? declineReason, IDbTransaction transaction);
}
