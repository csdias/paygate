using Microsoft.Extensions.DependencyInjection;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreDatabase(this IServiceCollection collection)
    {
        collection.AddTransient<IOutboxMessageChannel, DefaultOutboxMessageChannel>();
        collection.AddTransient<ITraceDataEnricher, TraceDataEnricher>();
        collection.AddTransient<IOutboxTableNames, OutboxTableNames>();
        return collection;
    }
}
