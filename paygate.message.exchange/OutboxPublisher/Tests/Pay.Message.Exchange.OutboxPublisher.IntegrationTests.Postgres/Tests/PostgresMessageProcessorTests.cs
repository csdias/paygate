using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Pay.Message.Exchange.OutboxClient;
using Pay.Message.Exchange.OutboxPublisher.Entities;
using Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.Fixtures;
using Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.TestData;

namespace Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.Tests;

[Collection(PostgresIntegrationTestCollectionFixture.Name)]
public class PostgresMessageProcessorTests : IAsyncLifetime
{
    private readonly PostgresIntegrationTestFixture _fixture;
    private PostgresBaseTestData _baseData;
    private PostgresMessageProcessorTestData _testData;
    private NpgsqlConnection _connection;

    public PostgresMessageProcessorTests(PostgresIntegrationTestFixture fixture)
        => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _baseData = new PostgresBaseTestData(
            _fixture.ClientProvider.GetRequiredService<IDbContext>(),
            _fixture.MigrationConnectionString,
            _fixture.TableNames);

        _testData = new PostgresMessageProcessorTestData(_fixture.TableNames);

        await _baseData.SeedDatabaseAsync();

        _connection = new NpgsqlConnection(_fixture.AppConnectionString);
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task ProcessMessages_SingleMessage_PublishedToSnsAndMarkedPublished()
    {
        var registry = _baseData.MessageRegistryEntries.First();
        var message = _testData.BuildOutboxMessage(registry);
        await _testData.InsertOutboxMessageAsync(_connection, message);

        _fixture.SetupSuccessfulNotificationService();

        await _fixture.RunOutboxPublisher(TimeSpan.FromSeconds(2));

        _fixture.PublishedMessages.Should().ContainSingle();
        var published = _fixture.PublishedMessages.Single().GetMessage();
        published.Payload.ToString().Should().Contain("paymentId");
        published.Metadata.ContextId.Should().Be(message.ContextId);
    }

    [Fact]
    public async Task ProcessMessages_MessageWithPredecessor_PublishedInOrder()
    {
        var registry = _baseData.MessageRegistryEntries.First();
        var first = _testData.BuildOutboxMessage(registry);
        var second = _testData.BuildOutboxMessage(registry, predecessorId: first.MessageId);

        await _testData.InsertOutboxMessageAsync(_connection, first);
        await _testData.InsertOutboxMessageAsync(_connection, second);

        _fixture.SetupSuccessfulNotificationService();

        await _fixture.RunOutboxPublisher(TimeSpan.FromSeconds(3));

        _fixture.PublishedMessages.Should().HaveCount(2);
        var bodies = _fixture.PublishedMessages.GetMessages().ToList();
        bodies[0].Metadata.ContextId.Should().Be(first.ContextId);
        bodies[1].Metadata.ContextId.Should().Be(second.ContextId);
    }

    [Fact]
    public async Task ProcessMessages_MessageWithFilters_SnsReceivesMessageAttributes()
    {
        var registry = _baseData.MessageRegistryEntries.First();
        var message = _testData.BuildOutboxMessageWithFilters(registry);
        await _testData.InsertOutboxMessageAsync(_connection, message);

        _fixture.SetupSuccessfulNotificationService();

        await _fixture.RunOutboxPublisher(TimeSpan.FromSeconds(2));

        _fixture.PublishedMessages.Should().ContainSingle();
        var request = _fixture.PublishedMessages.Single();
        request.MessageAttributes.Should().ContainKey("tenant");
        request.MessageAttributes["tenant"].StringValue.Should().Be("acme");
        request.MessageAttributes.Should().ContainKey("environment");
    }

    [Fact]
    public async Task ProcessMessages_SnsPublishFails_MessageRetried()
    {
        var registry = _baseData.MessageRegistryEntries.First();
        var message = _testData.BuildOutboxMessage(registry);
        await _testData.InsertOutboxMessageAsync(_connection, message);

        _fixture.SetupFailingNotificationService();

        await _fixture.RunOutboxPublisher(TimeSpan.FromSeconds(2));

        _fixture.NotificationService.Verify(
            x => x.PublishAsync(
                It.Is<Amazon.SimpleNotificationService.Model.PublishRequest>(r =>
                    r.TopicArn == registry.Topic),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessages_CancellationRequested_GracefullyStops()
    {
        var registry = _baseData.MessageRegistryEntries.First();
        var message = _testData.BuildOutboxMessage(registry);
        await _testData.InsertOutboxMessageAsync(_connection, message);

        _fixture.SetupSuccessfulNotificationService();

        var cts = new CancellationTokenSource();
        var runTask = _fixture.RunOutboxPublisher(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(runTask, "the publisher should stop within 5 seconds of cancellation");
    }
}
