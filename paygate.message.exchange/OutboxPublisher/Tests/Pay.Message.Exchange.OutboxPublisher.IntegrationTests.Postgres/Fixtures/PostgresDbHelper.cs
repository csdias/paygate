using System.Text.Json;
using Amazon.SimpleNotificationService.Model;
using Npgsql;
using Dapper;
using Pay.Message.Exchange.OutboxPublisher.Sns.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.IntegrationTests.Postgres.Fixtures;

public static class PostgresDbHelper
{
    public static async Task ExecuteCommands(string connectionString, string[] commands)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        foreach (var command in commands)
            await connection.ExecuteAsync(command);
    }

    public static SnsMessage GetMessage(this PublishRequest request)
        => JsonSerializer.Deserialize<SnsMessage>(request.Message);

    public static IEnumerable<SnsMessage> GetMessages(this IEnumerable<PublishRequest> requests)
    {
        foreach (var request in requests)
            yield return request.GetMessage();
    }
}
