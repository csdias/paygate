using Microsoft.Extensions.DependencyInjection;
using Paygate.Data;
using Paygate.Domain;

namespace Paygate.Data.Postgres;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UsePostgresPayment(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, PostgresDbConnectionFactory>();
        services.AddScoped<IPaymentRepository, PostgresPaymentRepository>();
        services.AddScoped<ITransactionalOutboxWriter, PostgresOutboxWriter>();
        return services;
    }
}
