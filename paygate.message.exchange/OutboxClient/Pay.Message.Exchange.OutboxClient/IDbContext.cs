using System.Data;

namespace Pay.Message.Exchange.OutboxClient;

public interface IDbContext : IDisposable
{
    IDbConnection DbConnection { get; }
    IDbTransaction CurrentTransaction { get; }
    IFilterRepository FilterRepository { get; }
    IMessageRegistryRepository MessageRegistryRepository { get; }
    IOutboxMessageRepository OutboxMessageRepository { get; }
    void BeginTransaction();
    void CommitTransaction();
    void RollBackTransaction();
}
