using Microsoft.Extensions.DependencyInjection;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Sns;

namespace Pay.Message.Exchange.OutboxPublisher.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection collection)
    {
        collection.AddTransient<IMessageProcessor, ChannelMessageProcessor>();
        collection.AddCoreDatabase();
        collection.AddSnsOutbox();
        return collection;
    }
}
