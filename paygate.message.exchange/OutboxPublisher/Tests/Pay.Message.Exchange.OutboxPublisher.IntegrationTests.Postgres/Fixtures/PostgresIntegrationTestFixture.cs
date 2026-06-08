using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Npgsql;
using Serilog;
using Testcontainers.PostgreSql;
using Pay.Message.Exchange.OutboxClient;
using Pay.Message.Exchange.OutboxClient.Postgres;
using Pay.Message.Exchange.OutboxPublisher.Db;

namespace Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.Fixtures;

public class PostgresIntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgresContainer;
    private Dictionary<string, string> _inMemoryConfig;
    private List<PublishRequest> _publishedMessages;
    private ServiceProvider _clientProvider;

    public string AppConnectionString { get; private set; }
    public string MigrationConnectionString { get; private set; }
    public IOutboxTableNames TableNames { get; private set; }
    public IServiceProvider ClientProvider => _clientProvider;
    public IReadOnlyList<PublishRequest> PublishedMessages => _publishedMessages;
    public Mock<IAmazonSimpleNotificationService> NotificationService { get; } = new();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("db")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _postgresContainer.StartAsync();

        var appConnBuilder = new NpgsqlConnectionStringBuilder(_postgresContainer.GetConnectionString())
        {
            Database = "test_outbox",
            Username = "test_outbox_user",
            Password = "test_outbox_password"
        };

        MigrationConnectionString = new NpgsqlConnectionStringBuilder(_postgresContainer.GetConnectionString())
        {
            Database = appConnBuilder.Database
        }.ConnectionString;

        await SetupDatabase(appConnBuilder);
        await SetupUserPrivileges(appConnBuilder, MigrationConnectionString);

        AppConnectionString = appConnBuilder.ConnectionString;
        SetupInMemoryConfig(AppConnectionString);

        var clientConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(_inMemoryConfig)
            .Build();

        var clientCollection = new ServiceCollection();
        ConfigureClientServices(clientCollection, clientConfig);
        _clientProvider = clientCollection.BuildServiceProvider();
        TableNames = _clientProvider.GetRequiredService<IOutboxTableNames>();

        SetupSuccessfulNotificationService();
    }

    public async Task DisposeAsync()
    {
        await _clientProvider.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    public void SetupSuccessfulNotificationService()
    {
        _publishedMessages = new List<PublishRequest>();
        NotificationService
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((req, _) => _publishedMessages.Add(req))
            .ReturnsAsync(() => new PublishResponse { MessageId = Guid.NewGuid().ToString() });
    }

    public void SetupFailingNotificationService()
    {
        NotificationService
            .Setup(x => x.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = null });
    }

    public void SetupInMemoryConfig(string appConnectionString)
    {
        _inMemoryConfig = new Dictionary<string, string>
        {
            { "ConnectionStrings:local", appConnectionString },
            { "OutboxDatabase:BatchSize", "10" },
            { "OutboxDatabase:InactiveDelay", "0:0:10" },
            { "OutboxDatabase:SleepDelay", "0:0:0.0100000" },
            { "OutboxDatabase:TableName", "outbox_test" }
        };
    }

    public async Task RunOutboxPublisher(TimeSpan runDuration)
    {
        var cancellation = new CancellationTokenSource();
        var runTask = RunOutboxPublisher(cancellation.Token);
        await Task.Delay(runDuration);
        cancellation.Cancel();
        await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(1)));
        if (!runTask.IsCompleted)
            throw new InvalidOperationException("Outbox publisher ran for longer than expected");
    }

    public Task RunOutboxPublisher(CancellationToken cancellation)
    {
        _publishedMessages = new List<PublishRequest>();
        Program.ConfigureSerilog();

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(ConfigureApplication)
            .ConfigureAppConfiguration(Program.ConfigureApplication)
            .ConfigureServices(Program.ConfigureServices)
            .ConfigureServices(OverrideServices)
            .UseSerilog()
            .Build();

        return host.RunAsync(cancellation);
    }

    private void ConfigureApplication(HostBuilderContext context, IConfigurationBuilder configBuilder)
        => configBuilder.AddInMemoryCollection(_inMemoryConfig);

    private void OverrideServices(HostBuilderContext context, IServiceCollection services)
    {
        services.RemoveAll<IAmazonSimpleNotificationService>();
        services.AddSingleton(NotificationService.Object);
    }

    private static IServiceCollection ConfigureClientServices(IServiceCollection collection, IConfiguration config)
    {
        collection.Configure<DatabaseOptions>(config.GetSection("OutboxDatabase"));
        collection.AddSingleton(config);
        collection.UsePostgresOutboxClient();
        collection.AddOutboxClient();
        collection.AddLogging();
        return collection;
    }

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

    private async Task SetupUserPrivileges(NpgsqlConnectionStringBuilder appConnBuilder,
        string migrationConnectionString)
    {
        await using var conn = new NpgsqlConnection(migrationConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        foreach (var sql in new[]
        {
            "REVOKE CREATE ON SCHEMA public FROM public;",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, UPDATE, INSERT, DELETE ON TABLES TO {appConnBuilder.Username};",
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, USAGE ON SEQUENCES TO {appConnBuilder.Username};"
        })
        {
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
