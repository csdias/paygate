using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient;

public interface IFilterRepository
{
    Task<IReadOnlyCollection<MessageFilter>> GetFiltersForMessage(Guid messageId);
    Task CreateFilters(IEnumerable<MessageFilter> filters);
}
