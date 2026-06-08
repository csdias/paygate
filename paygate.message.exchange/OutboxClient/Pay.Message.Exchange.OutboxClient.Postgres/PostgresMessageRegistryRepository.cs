using Dapper;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient.Postgres;

public class PostgresMessageRegistryRepository : IMessageRegistryRepository
{
    private readonly IDbContext _context;
    private readonly IOutboxTableNames _tableNames;
    private readonly string _selectAllSql;
    private readonly string _selectByIdSql;

    public PostgresMessageRegistryRepository(IDbContext context, IOutboxTableNames tableNames)
    {
        _context = context;
        _tableNames = tableNames;
        _selectAllSql = $@"
SELECT r.message_registry_id      AS MessageRegistryId,
       r.message_name             AS MessageName,
       r.message_version          AS MessageVersion,
       r.message_type_id          AS MessageTypeId,
       r.topic                    AS Topic,
       r.retry_backoff_in_seconds AS RetryBackoffInSeconds,
       r.retry_limit              AS RetryLimit,
       t.message_type_id          AS MessageTypeId,
       t.message_type_desc        AS MessageTypeDescription
FROM {_tableNames.MessageRegistry} r
    INNER JOIN {_tableNames.MessageType} t ON r.message_type_id = t.message_type_id
";
        _selectByIdSql = _selectAllSql + "WHERE r.message_registry_id = :id";
    }

    public async Task<IReadOnlyCollection<MessageRegistry>> GetAllAsync()
    {
        var messages = await _context.DbConnection.QueryAsync<MessageRegistry, MessageType, MessageRegistry>(
            _selectAllSql, (registry, type) => { registry.MessageType = type; return registry; },
            splitOn: "MessageTypeId");
        return messages.AsList();
    }

    public async Task<MessageRegistry> GetByIdAsync(Guid id)
    {
        var messages = await _context.DbConnection.QueryAsync<MessageRegistry, MessageType, MessageRegistry>(
            _selectByIdSql, (registry, type) => { registry.MessageType = type; return registry; },
            splitOn: "MessageTypeId", param: new { id });
        return messages.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(MessageRegistry message)
    {
        var guid = Guid.NewGuid();
        var sql = $@"
INSERT INTO {_tableNames.MessageRegistry}
    (message_registry_id, message_name, message_version, message_type_id, topic, retry_limit, retry_backoff_in_seconds)
VALUES (:id, :name, :version, :typeId, :topic, :retryLimit, :retryBackoff)
";
        await _context.DbConnection.ExecuteAsync(sql, new
        {
            id = guid,
            name = message.MessageName,
            version = message.MessageVersion,
            typeId = message.MessageTypeId,
            topic = message.Topic,
            retryLimit = message.RetryLimit,
            retryBackoff = message.RetryBackoffInSeconds
        }, _context.CurrentTransaction);

        return guid;
    }

    public async Task<bool> UpdateAsync(MessageRegistry message)
    {
        var sql = $@"
UPDATE {_tableNames.MessageRegistry}
SET message_name             = :name,
    message_version          = :version,
    message_type_id          = :typeId,
    topic                    = :topic,
    retry_limit              = :retryLimit,
    retry_backoff_in_seconds = :retryBackoff
WHERE message_registry_id = :id
";
        var modified = await _context.DbConnection.ExecuteAsync(sql, new
        {
            id = message.MessageRegistryId,
            name = message.MessageName,
            version = message.MessageVersion,
            typeId = message.MessageTypeId,
            topic = message.Topic,
            retryLimit = message.RetryLimit,
            retryBackoff = message.RetryBackoffInSeconds
        }, _context.CurrentTransaction);

        return modified > 0;
    }
}
