using System.Diagnostics;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Db.Postgres;
using Pay.Message.Exchange.OutboxPublisher.Services;

namespace Pay.Message.Exchange.OutboxPublisher;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        ConfigureSerilog();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(ConfigureApplication)
                .ConfigureServices(ConfigureServices)
                .UseSerilog()
                .Build();

            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static void ConfigureApplication(HostBuilderContext context, IConfigurationBuilder config)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Development")
            config.AddSystemsManager(src =>
            {
                src.Path =
                    $"/aws/reference/secretsmanager/{Environment.GetEnvironmentVariable("SecretsManagerRdsUserCredentials")}";
                src.ReloadAfter = TimeSpan.FromHours(24);
            });
    }

    public static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
    {
        services.Configure<DatabaseOptions>(hostContext.Configuration.GetSection("OutboxDatabase"));

        // Register AWS options from the "AWS" config section so AddAWSService picks up
        // Region and (locally) ServiceURL. In production there is no AWS section, so this
        // resolves to standard region/credential discovery against real AWS.
        services.AddDefaultAWSOptions(hostContext.Configuration.GetAWSOptions());

        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") != "Development")
            services.Configure<DatabaseConnectionDetails>(
                hostContext.Configuration.GetSection(
                    Environment.GetEnvironmentVariable("SecretsManagerRdsUserCredentials")));

        services.AddOutboxPublisher();
        services.AddPostgresOutbox();
        services.AddHostedService<OutboxPublisherHostedService>();
    }

    public static void ConfigureSerilog()
    {
        var logConfig = new LoggerConfiguration();

        if (Environment.GetEnvironmentVariable("LOG_LEVEL_OVERRIDE") is { } logLevel &&
            Enum.TryParse(logLevel, true, out LogEventLevel level))
        {
            logConfig = logConfig.MinimumLevel.Is(level);
        }
        else
        {
            logConfig = logConfig
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Information)
                .Filter.ByExcluding(
                    "StartsWith(SourceContext, 'System.Net.Http.HttpClient') and not (EventId.Id = 101 and EndsWith(SourceContext, 'ClientHandler'))");
        }

        var loggerConfig = logConfig
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .Enrich.FromLogContext()
            .Enrich.WithSpan();

        // Forward to the Signals panel when configured (local dev); no-op otherwise.
        var ingestUrl = Environment.GetEnvironmentVariable("TELEMETRY_INGEST_URL");
        if (!string.IsNullOrWhiteSpace(ingestUrl))
            loggerConfig = loggerConfig.WriteTo.Sink(new IngestSink(ingestUrl, "OutboxPublisher"));

        Log.Logger = loggerConfig.CreateLogger();
    }
}
