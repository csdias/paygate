using Dapper;
using Npgsql;

namespace Pay.Message.Exchange.OutboxClient.IntegrationTests.Fixtures;

public class PostgresIntegrationTestFixture : BaseTestFixture
{
    protected override async Task SeedAsync()
    {
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
                "Resources", "Sql", "Postgres", "outbox_creation.sql"));

        await using var connection = new NpgsqlConnection(MigrationConnectionString);
        await connection.ExecuteAsync(sql);

        await using var seedConn = new NpgsqlConnection(AppConnectionString);
        await seedConn.ExecuteAsync(@"
INSERT INTO message_type (message_type_id, message_type_name)
VALUES (1, 'Event'), (2, 'Command')
ON CONFLICT DO NOTHING;
");
    }
}
