using Amazon.SimpleNotificationService;
using Microsoft.Extensions.DependencyInjection;

namespace Pay.Message.Exchange.OutboxPublisher.Sns;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSnsOutbox(this IServiceCollection collection)
    {
        collection.AddTransient<IMessagePublisher, SnsMessagePublisher>();
        collection.AddAWSService<IAmazonSimpleNotificationService>();
        collection.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
        return collection;
    }
}
