using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Pay.Message.Exchange.OutboxPublisher.Db.Postgres;

public class PostgresDbConnectionFactory : IDbConnectionFactory
{
    private readonly ILogger<PostgresDbConnectionFactory> _logger;
    private readonly string _connectionString;

    public PostgresDbConnectionFactory(IConfiguration config, IOptions<DatabaseOptions> options,
        IOptions<DatabaseConnectionDetails> connectionDetailsOptions,
        ILogger<PostgresDbConnectionFactory> logger)
    {
        var databaseOptions = options.Value;
        var connectionDetails = connectionDetailsOptions.Value;
        _logger = logger;
        _connectionString = GetConnectionString(databaseOptions, connectionDetails, config);
    }

    public IDbConnection GetConnection()
    {
        _logger.LogTrace("Creating new connection");
        return new NpgsqlConnection(_connectionString);
    }

    private static string GetConnectionString(DatabaseOptions options, DatabaseConnectionDetails connectionDetails,
        IConfiguration config)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development" ||
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            return config["ConnectionStrings:local"];
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = connectionDetails.Host,
            Port = connectionDetails.Port,
            Username = connectionDetails.Username,
            Database = options.DatabaseName,
            Password = connectionDetails.Password,
            MaxPoolSize = options.MaxConnectionPoolSize
        }.ConnectionString;
    }
}
