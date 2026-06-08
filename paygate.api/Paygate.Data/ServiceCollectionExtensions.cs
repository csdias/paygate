using Microsoft.Extensions.DependencyInjection;

namespace Paygate.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.AddScoped<PaymentService>();
        return services;
    }
}
