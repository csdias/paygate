using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Paygate.Domain;

namespace Paygate.Data.Postgres;

public class PostgresDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public PostgresDbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("local")
            ?? throw new InvalidOperationException("Connection string 'local' is not configured.");
    }

    public IDbConnection Create() => new NpgsqlConnection(_connectionString);
}
