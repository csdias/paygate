using Dapper;
using Npgsql;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient.IntegrationTests.TestData.Postgres;

public static class MessageRegistrySeedData
{
    public static MessageRegistry Build(int messageTypeId = 1) => new()
    {
        MessageRegistryId = Guid.NewGuid(),
        MessageName = $"TestEvent_{Guid.NewGuid():N}",
        MessageVersion = "1.0",
        MessageTypeId = messageTypeId,
        Topic = "arn:aws:sns:eu-west-1:000000000000:test-topic",
        RetryLimit = 3,
        RetryBackoffInSeconds = 60
    };

    public static async Task InsertAsync(NpgsqlConnection connection, MessageRegistry registry)
    {
        await connection.ExecuteAsync(@"
INSERT INTO message_registry
    (message_registry_id, message_name, message_version, message_type_id, topic, retry_limit, retry_backoff_in_seconds)
VALUES
    (@MessageRegistryId, @MessageName, @MessageVersion, @MessageTypeId, @Topic, @RetryLimit, @RetryBackoffInSeconds)",
            registry);
    }
}
