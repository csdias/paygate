using Microsoft.Extensions.DependencyInjection;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Db.Postgres;

namespace Pay.Message.Exchange.OutboxClient.Postgres;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UsePostgresOutboxClient(this IServiceCollection collection)
    {
        collection.AddTransient<IDbConnectionFactory, PostgresDbConnectionFactory>();
        collection.AddScoped<IDbContext, PostgresOutboxClientContext>();
        // PaymentService injects IMessageRegistryRepository directly (not via IDbContext),
        // so it needs a standalone registration.
        collection.AddScoped<IMessageRegistryRepository, PostgresMessageRegistryRepository>();
        collection.AddPostgresOutbox();
        collection.AddCoreDatabase();
        return collection;
    }
}
