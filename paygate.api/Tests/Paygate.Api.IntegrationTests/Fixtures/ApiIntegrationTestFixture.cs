using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using Pay.Message.Exchange.OutboxClient;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Paygate.Api.IntegrationTests.Fixtures;

public class ApiIntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgresContainer = default!;
    private WebApplicationFactory<Program> _factory = default!;

    public HttpClient Client { get; private set; } = default!;
    public string AppConnectionString { get; private set; } = default!;
    public Mock<IOutboxService> OutboxServiceMock { get; } = new();

    public async Task InitializeAsync()
    {
        // PostgresDbConnectionFactory reads the *process* env var (not IConfiguration)
        // to decide whether to use ConnectionStrings:local. Set it so the Development
        // branch is taken, matching how the app runs locally.
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("paygate_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _postgresContainer.StartAsync();

        AppConnectionString = _postgresContainer.GetConnectionString();

        await RunMigrationsAsync();
        await SeedRegistryAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "ConnectionStrings:local", AppConnectionString },
                        { "OutboxDatabase:TableName", "outbox" },
                        { "DOTNET_ENVIRONMENT", "Development" }
                    });
                });
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IOutboxService>();
                    services.AddScoped(_ => OutboxServiceMock.Object);

                    // Swap real JWT bearer for the header-driven test scheme (set as default,
                    // so the endpoints' RequireAuthorization policies authenticate against it).
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                });
            });

        Client = _factory.CreateClient();   // anonymous (no persona headers) → 401 on protected routes
    }

    // Persona scopes/roles mirror the IdentityServer client + user config.
    public HttpClient AsClerk() =>
        ClientAs("clerk", "PaymentInitiator", "payments.read payments.write");

    public HttpClient AsApprover() =>
        ClientAs("approver", "PaymentApprover", "payments.read payments.approve");

    public HttpClient AsAuditor() =>
        ClientAs("auditor", "Auditor", "payments.read");

    // A single principal holding both create and approve rights — used only to prove the
    // maker-checker rule blocks self-approval (same sub created and approved).
    public HttpClient AsMakerChecker(string sub) =>
        ClientAs(sub, "PaymentInitiator,PaymentApprover", "payments.read payments.write payments.approve");

    public HttpClient ClientAs(string sub, string roles, string scopes)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        client.DefaultRequestHeaders.Add("X-Test-Role", roles);
        client.DefaultRequestHeaders.Add("X-Test-Scope", scopes);
        return client;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    public async Task<int> CountOutboxRowsAsync(string contextId)
    {
        await using var conn = new NpgsqlConnection(AppConnectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE context_id = @ContextId",
            new { ContextId = contextId });
    }

    public async Task<int> CountPaymentRowsAsync(Guid paymentId)
    {
        await using var conn = new NpgsqlConnection(AppConnectionString);
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM payment WHERE payment_id = @PaymentId",
            new { PaymentId = paymentId });
    }

    private async Task RunMigrationsAsync()
    {
        await using var conn = new NpgsqlConnection(AppConnectionString);

        var outboxSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..",
                "paygate.message.exchange", "Resources", "Sql", "Postgres", "outbox_creation.sql"));

        var paymentSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "Resources", "Sql", "Postgres", "payment_creation.sql"));

        await conn.ExecuteAsync(outboxSql);
        await conn.ExecuteAsync(paymentSql);
    }

    private async Task SeedRegistryAsync()
    {
        await using var conn = new NpgsqlConnection(AppConnectionString);
        await conn.ExecuteAsync(@"
INSERT INTO outbox_message_registry
    (message_registry_id, message_name, message_version, message_type_id, topic, retry_limit, retry_backoff_in_seconds)
VALUES
    (gen_random_uuid(), 'PaymentInitiatedEvent', '1.0', 1,
     'arn:aws:sns:eu-west-1:000000000000:payment-topic', 3, 60),
    (gen_random_uuid(), 'PaymentDecidedEvent', '1.0', 1,
     'arn:aws:sns:eu-west-1:000000000000:payment-topic', 3, 60)
ON CONFLICT DO NOTHING");
    }
}
