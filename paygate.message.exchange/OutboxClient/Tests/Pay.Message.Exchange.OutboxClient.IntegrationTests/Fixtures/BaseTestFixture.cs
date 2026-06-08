using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Pay.Message.Exchange.OutboxClient;
using Pay.Message.Exchange.OutboxClient.Postgres;
using Pay.Message.Exchange.OutboxPublisher.Db;

namespace Pay.Message.Exchange.OutboxClient.IntegrationTests.Fixtures;

public abstract class BaseTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgresContainer;
    protected ServiceProvider Provider;

    public string AppConnectionString { get; private set; }
    public string MigrationConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _postgresContainer.StartAsync();

        var appConnBuilder = new NpgsqlConnectionStringBuilder(_postgresContainer.GetConnectionString())
        {
            Database = "test_outbox_client",
            Username = "test_client_user",
            Password = "test_client_password"
        };

        MigrationConnectionString = new NpgsqlConnectionStringBuilder(_postgresContainer.GetConnectionString())
        {
            Database = appConnBuilder.Database
        }.ConnectionString;

        await SetupDatabase(appConnBuilder);
        await SetupUserPrivileges(appConnBuilder.Username, MigrationConnectionString);

        AppConnectionString = appConnBuilder.ConnectionString;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ConnectionStrings:local", AppConnectionString },
                { "OutboxDatabase:TableName", "outbox" }
            })
            .Build();

        var collection = new ServiceCollection();
        collection.AddSingleton<IConfiguration>(config);
        collection.Configure<DatabaseOptions>(config.GetSection("OutboxDatabase"));
        collection.UsePostgresOutboxClient();
        collection.AddOutboxClient();
        collection.AddLogging();

        Provider = collection.BuildServiceProvider();

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await Provider.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    protected abstract Task SeedAsync();

    private async Task SetupDatabase(NpgsqlConnectionStringBuilder appConnBuilder)
    {
        await using var conn = new NpgsqlConnection(_postgresContainer.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        foreach (var sql in new[]
        {
            $"CREATE USER {appConnBuilder.Username} WITH PASSWORD '{appConnBuilder.Password}';",
            $"CREATE DATABASE {appConnBuilder.Database};",
            $"REVOKE ALL ON DATABASE {appConnBuilder.Database} FROM public;",
            $"GRANT CONNECT ON DATABASE {appConnBuilder.Database} TO {appConnBuilder.Username};"
        })
        {
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task SetupUserPrivileges(string username, string migrationConnectionString)
    {
        await using var conn = new NpgsqlConnection(migrationConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        foreach (var sql in new[]
        {
            "REVOKE CREATE ON SCHEMA public FROM public;",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, UPDATE, INSERT, DELETE ON TABLES TO {username};",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, USAGE ON SEQUENCES TO {username};"
        })
        {
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
