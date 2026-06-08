namespace Pay.Message.Exchange.OutboxPublisher.Db;

public interface IOutboxTableNames
{
    string Outbox { get; }
    string MessageRegistry { get; }
    string MessageType { get; }
    string MessageFilter { get; }
}
