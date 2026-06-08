using Pay.Message.Exchange.OutboxPublisher.Db;

namespace Pay.Message.Exchange.OutboxClient.Postgres;

public class PostgresOutboxClientContext : OutboxClientContext
{
    public PostgresOutboxClientContext(IDbConnectionFactory connectionFactory, IOutboxTableNames tableNames)
        : base(connectionFactory)
    {
        FilterRepository = new PostgresFilterRepository(this, tableNames);
        MessageRegistryRepository = new PostgresMessageRegistryRepository(this, tableNames);
        OutboxMessageRepository = new PostgresOutboxMessageRepository(this, tableNames);
    }

    public override IFilterRepository FilterRepository { get; }
    public override IMessageRegistryRepository MessageRegistryRepository { get; }
    public override IOutboxMessageRepository OutboxMessageRepository { get; }
}
