using Microsoft.Extensions.Options;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public class OutboxTableNames : IOutboxTableNames
{
    private readonly string _baseName;

    public OutboxTableNames(IOptions<DatabaseOptions> options)
    {
        _baseName = options.Value.TableName;
    }

    public string Outbox => _baseName;
    public string MessageRegistry => $"{_baseName}_message_registry";
    public string MessageType => $"{_baseName}_message_type";
    public string MessageFilter => $"{_baseName}_message_filter";
}
