using System.Data;
using Pay.Message.Exchange.OutboxPublisher.Db;

namespace Pay.Message.Exchange.OutboxClient;

public abstract class OutboxClientContext : IDbContext
{
    private readonly Lazy<IDbConnection> _createConnection;

    protected OutboxClientContext(IDbConnectionFactory connectionFactory)
    {
        _createConnection = new Lazy<IDbConnection>(connectionFactory.GetConnection);
    }

    public void Dispose()
    {
        CurrentTransaction?.Dispose();
        if (_createConnection.IsValueCreated)
            _createConnection.Value?.Dispose();
    }

    public IDbConnection DbConnection => _createConnection.Value;
    public IDbTransaction CurrentTransaction { get; private set; }
    public abstract IFilterRepository FilterRepository { get; }
    public abstract IMessageRegistryRepository MessageRegistryRepository { get; }
    public abstract IOutboxMessageRepository OutboxMessageRepository { get; }

    public void BeginTransaction()
    {
        if (DbConnection.State != ConnectionState.Open)
            DbConnection.Open();
        CurrentTransaction = DbConnection.BeginTransaction();
    }

    public void CommitTransaction()
    {
        CurrentTransaction.Commit();
        CurrentTransaction.Dispose();
        CurrentTransaction = null;
    }

    public void RollBackTransaction()
    {
        CurrentTransaction.Rollback();
        CurrentTransaction.Dispose();
        CurrentTransaction = null;
    }
}
