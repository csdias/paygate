using Dapper;
using FluentAssertions;
using Npgsql;
using Pay.Message.Exchange.OutboxClient.IntegrationTests.Fixtures;
using Pay.Message.Exchange.OutboxClient.IntegrationTests.TestData.Postgres;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient.IntegrationTests.Tests;

public class MessageRegistryTests : BaseTest
{
    private readonly IMessageRegistryRepository _repository;

    public MessageRegistryTests(PostgresIntegrationTestFixture fixture) : base(fixture)
        => _repository = GetService<IMessageRegistryRepository>();

    [Fact]
    public async Task GetAllAsync_ReturnsRegisteredEntries()
    {
        await using var conn = new NpgsqlConnection(Fixture.AppConnectionString);
        var registry = MessageRegistrySeedData.Build();
        await MessageRegistrySeedData.InsertAsync(conn, registry);

        var all = await _repository.GetAllAsync();

        all.Should().Contain(r => r.MessageName == registry.MessageName);
    }

    [Fact]
    public async Task InsertAsync_ThenGetAll_ContainsNewEntry()
    {
        var registry = new MessageRegistry
        {
            MessageRegistryId = Guid.NewGuid(),
            MessageName = $"NewEvent_{Guid.NewGuid():N}",
            MessageVersion = "2.0",
            MessageTypeId = 1,
            Topic = "arn:aws:sns:eu-west-1:000000000000:new-topic",
            RetryLimit = 5,
            RetryBackoffInSeconds = 30
        };

        await _repository.InsertAsync(registry);

        var all = await _repository.GetAllAsync();
        all.Should().Contain(r => r.MessageRegistryId == registry.MessageRegistryId);
    }
}
