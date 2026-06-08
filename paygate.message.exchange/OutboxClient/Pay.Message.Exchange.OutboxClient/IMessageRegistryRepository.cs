using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient;

public interface IMessageRegistryRepository
{
    Task<IReadOnlyCollection<MessageRegistry>> GetAllAsync();
    Task<MessageRegistry> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(MessageRegistry message);
    Task<bool> UpdateAsync(MessageRegistry message);
}
