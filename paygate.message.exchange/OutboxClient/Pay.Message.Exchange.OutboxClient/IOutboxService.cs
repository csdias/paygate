namespace Pay.Message.Exchange.OutboxClient;

public interface IOutboxService
{
    Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload);

    Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload,
        IEnumerable<KeyValuePair<string, string>> filters);

    Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload,
        string traceParent, string traceState);

    Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload,
        IEnumerable<KeyValuePair<string, string>> filters, string traceParent, string traceState);
}
