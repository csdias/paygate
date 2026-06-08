using Microsoft.Extensions.DependencyInjection;

namespace Pay.Message.Exchange.OutboxPublisher.Db.Postgres;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresOutbox(this IServiceCollection collection)
    {
        collection.AddTransient<IDbConnectionFactory, PostgresDbConnectionFactory>();
        collection.AddTransient<IOutbox, PostgresOutbox>();
        return collection;
    }
}
