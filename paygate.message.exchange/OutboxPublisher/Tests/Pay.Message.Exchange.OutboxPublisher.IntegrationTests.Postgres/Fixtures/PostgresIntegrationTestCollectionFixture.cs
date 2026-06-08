namespace Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.Fixtures;

[CollectionDefinition(Name)]
public class PostgresIntegrationTestCollectionFixture : ICollectionFixture<PostgresIntegrationTestFixture>
{
    public const string Name = nameof(PostgresIntegrationTestFixture);
}
