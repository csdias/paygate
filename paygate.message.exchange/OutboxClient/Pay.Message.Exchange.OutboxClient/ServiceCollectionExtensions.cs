using Microsoft.Extensions.DependencyInjection;
using Pay.Message.Exchange.OutboxPublisher.Db;

namespace Pay.Message.Exchange.OutboxClient;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxClient(this IServiceCollection collection)
    {
        collection.AddTransient<IOutboxService, DefaultOutboxService>();
        collection.AddTransient<IOutboxTableNames, OutboxTableNames>();
        return collection;
    }
}
