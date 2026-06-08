using System.Data;
using Dapper;

namespace Paygate.Audit;

public class AuditRepository
{
    private readonly IDbConnection _connection;

    public AuditRepository(IDbConnection connection) => _connection = connection;

    public Task InsertAsync(AuditRecord record) =>
        _connection.ExecuteAsync(@"
INSERT INTO payment_audit (audit_id, payment_id, event_type, payload, occurred_at)
VALUES (@AuditId, @PaymentId, @EventType, @Payload::json, @OccurredAt)",
            record);
}
