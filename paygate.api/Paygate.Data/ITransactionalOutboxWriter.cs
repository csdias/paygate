using System.Data;

namespace Paygate.Data;

public interface ITransactionalOutboxWriter
{
    Task WriteAsync(Guid messageRegistryId, string contextId, string messageBody, IDbTransaction transaction);
}
