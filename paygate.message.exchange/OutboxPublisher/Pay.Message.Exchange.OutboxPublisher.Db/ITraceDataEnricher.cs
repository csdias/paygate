using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Db;

public interface ITraceDataEnricher
{
    bool EnrichAndUpdateMessage(OutboxMessage outboxMessage);
}
