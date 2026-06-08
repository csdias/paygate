using Microsoft.Extensions.DependencyInjection;
using Pay.Message.Exchange.OutboxClient.IntegrationTests.Fixtures;

namespace Pay.Message.Exchange.OutboxClient.IntegrationTests;

[Collection(PostgresIntegrationTestCollectionFixture.Name)]
public abstract class BaseTest
{
    protected readonly PostgresIntegrationTestFixture Fixture;

    protected BaseTest(PostgresIntegrationTestFixture fixture)
        => Fixture = fixture;

    protected T GetService<T>() => Fixture.Provider.GetRequiredService<T>();
}
