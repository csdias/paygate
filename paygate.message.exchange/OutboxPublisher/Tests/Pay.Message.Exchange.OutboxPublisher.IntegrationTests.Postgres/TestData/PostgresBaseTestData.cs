using Dapper;
using Npgsql;
using Pay.Message.Exchange.OutboxClient;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.TestData;

public class PostgresBaseTestData
{
    private readonly IDbContext _dbContext;
    private readonly string _migrationConnectionString;
    private readonly IOutboxTableNames _tableNames;

    public IReadOnlyList<MessageRegistry> MessageRegistryEntries { get; private set; }

    public PostgresBaseTestData(IDbContext dbContext, string migrationConnectionString,
        IOutboxTableNames tableNames)
    {
        _dbContext = dbContext;
        _migrationConnectionString = migrationConnectionString;
        _tableNames = tableNames;
    }

    public async Task SeedDatabaseAsync()
    {
        await RunMigration();
        MessageRegistryEntries = await SeedMessageRegistry();
    }

    private async Task RunMigration()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
                "Resources", "Sql", "Postgres", "outbox_creation.sql"));

        var renamedSql = sql
            .Replace("outbox", _tableNames.Outbox, StringComparison.OrdinalIgnoreCase)
            .Replace($"{_tableNames.Outbox}_message_registry", _tableNames.MessageRegistry,
                StringComparison.OrdinalIgnoreCase)
            .Replace($"{_tableNames.Outbox}_message_type", _tableNames.MessageType,
                StringComparison.OrdinalIgnoreCase)
            .Replace($"{_tableNames.Outbox}_message_filter", _tableNames.MessageFilter,
                StringComparison.OrdinalIgnoreCase);

        await using var connection = new NpgsqlConnection(_migrationConnectionString);
        await connection.ExecuteAsync(renamedSql);
    }

    private async Task<IReadOnlyList<MessageRegistry>> SeedMessageRegistry()
    {
        var registryId = Guid.NewGuid();
        var insertSql = $@"
INSERT INTO {_tableNames.MessageRegistry}
    (message_registry_id, message_name, message_version, message_type_id, topic, retry_limit, retry_backoff_in_seconds)
VALUES
    ('{registryId}', 'PaymentInitiatedEvent', '1.0', 1, 'arn:aws:sns:eu-west-1:000000000000:payment-topic', 3, 60)
";
        _dbContext.BeginTransaction();
        await _dbContext.DbConnection.ExecuteAsync(insertSql, transaction: _dbContext.CurrentTransaction);
        _dbContext.CommitTransaction();

        return await _dbContext.MessageRegistryRepository.GetAllAsync() as IReadOnlyList<MessageRegistry>
               ?? new List<MessageRegistry>();
    }
}
