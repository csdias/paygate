namespace Pay.Message.Exchange.OutboxClient.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public class PostgresIntegrationTestCollectionFixture : ICollectionFixture<PostgresIntegrationTestFixture>
{
    public const string Name = nameof(PostgresIntegrationTestFixture);
}
