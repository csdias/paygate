using System.Data.Common;
using Dapper;
using Pay.Message.Exchange.OutboxPublisher.Db;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient.Postgres;

public class PostgresFilterRepository : IFilterRepository
{
    private readonly IDbContext _context;
    private readonly IOutboxTableNames _tableNames;

    public PostgresFilterRepository(IDbContext context, IOutboxTableNames tableNames)
    {
        _context = context;
        _tableNames = tableNames;
    }

    public async Task<IReadOnlyCollection<MessageFilter>> GetFiltersForMessage(Guid messageId)
    {
        var sql = $@"
SELECT f.message_id  AS MessageId,
       f.filter_key  AS FilterKey,
       f.filter_value AS FilterValue
FROM {_tableNames.MessageFilter} f
WHERE f.message_id = :id
";
        var results = await _context.DbConnection.QueryAsync<MessageFilter>(sql, new { id = messageId });
        return results.AsList();
    }

    public async Task CreateFilters(IEnumerable<MessageFilter> filters)
    {
        var sql = $@"
INSERT INTO {_tableNames.MessageFilter} (message_id, filter_key, filter_value)
VALUES (:messageId, :filterKey, :filterValue)
";
        var autocommit = _context.CurrentTransaction is null;
        var filterParams = filters.Select(f => new
        {
            messageId = f.MessageId,
            filterKey = f.FilterKey,
            filterValue = f.FilterValue
        }).ToArray();

        if (autocommit) _context.BeginTransaction();

        try
        {
            await _context.DbConnection.ExecuteAsync(sql, filterParams, _context.CurrentTransaction);
            if (autocommit) _context.CommitTransaction();
        }
        catch (DbException)
        {
            if (autocommit) _context.RollBackTransaction();
            throw;
        }
    }
}
