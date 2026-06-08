namespace Paygate.Api.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public class ApiIntegrationTestCollectionFixture : ICollectionFixture<ApiIntegrationTestFixture>
{
    public const string Name = nameof(ApiIntegrationTestFixture);
}
