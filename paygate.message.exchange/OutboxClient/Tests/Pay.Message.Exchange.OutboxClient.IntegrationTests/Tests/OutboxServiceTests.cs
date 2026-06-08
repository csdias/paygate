using Dapper;
using FluentAssertions;
using Npgsql;
using Pay.Message.Exchange.OutboxClient.IntegrationTests.Fixtures;
using Pay.Message.Exchange.OutboxClient.IntegrationTests.TestData.Postgres;

namespace Pay.Message.Exchange.OutboxClient.IntegrationTests.Tests;

public class OutboxServiceTests : BaseTest
{
    private readonly IOutboxService _outboxService;

    public OutboxServiceTests(PostgresIntegrationTestFixture fixture) : base(fixture)
        => _outboxService = GetService<IOutboxService>();

    [Fact]
    public async Task SendMessageAsync_ValidMessage_InsertsOutboxRow()
    {
        await using var conn = new NpgsqlConnection(Fixture.AppConnectionString);
        var registry = MessageRegistrySeedData.Build();
        await MessageRegistrySeedData.InsertAsync(conn, registry);

        var messageBody = """{"paymentId":"pay-001","amount":500}""";
        await _outboxService.SendMessageAsync(registry.MessageName, messageBody, contextId: "ctx-test");

        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM outbox WHERE context_id = 'ctx-test'");
        count.Should().Be(1);
    }

    [Fact]
    public async Task SendMessageAsync_WithFilters_InsertsFiltersRow()
    {
        await using var conn = new NpgsqlConnection(Fixture.AppConnectionString);
        var registry = MessageRegistrySeedData.Build();
        await MessageRegistrySeedData.InsertAsync(conn, registry);

        var filters = new Dictionary<string, string>
        {
            { "tenant", "acme" },
            { "env", "prod" }
        };

        await _outboxService.SendMessageAsync(registry.MessageName, """{"id":"1"}""",
            contextId: "ctx-filter", filters: filters);

        var filterCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*) FROM message_filter mf
INNER JOIN outbox o ON o.message_id = mf.message_id
WHERE o.context_id = 'ctx-filter'");
        filterCount.Should().Be(2);
    }

    [Fact]
    public async Task SendMessageAsync_UnknownMessageName_ThrowsException()
    {
        var act = () => _outboxService.SendMessageAsync("NonExistentMessage", """{}""", contextId: "x");
        await act.Should().ThrowAsync<Exception>();
    }
}
