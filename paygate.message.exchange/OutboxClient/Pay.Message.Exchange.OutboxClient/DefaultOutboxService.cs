using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pay.Message.Exchange.OutboxPublisher.Entities;

namespace Pay.Message.Exchange.OutboxClient;

public class DefaultOutboxService : IOutboxService
{
    private readonly IDbContext _context;
    private readonly ILogger<DefaultOutboxService> _logger;

    public DefaultOutboxService(IDbContext context, ILogger<DefaultOutboxService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload)
        => SendMessageAsync(messageRegistryId, contextId, payload, null, null, null);

    public Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload,
        IEnumerable<KeyValuePair<string, string>> filters)
        => SendMessageAsync(messageRegistryId, contextId, payload, filters, null, null);

    public Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload,
        string traceParent, string traceState)
        => SendMessageAsync(messageRegistryId, contextId, payload, null, traceParent, traceState);

    public async Task<Guid> SendMessageAsync<T>(Guid messageRegistryId, string contextId, T payload,
        IEnumerable<KeyValuePair<string, string>> filters, string traceParent, string traceState)
    {
        var insertedAt = DateTimeOffset.UtcNow;
        var serializedPayload = JsonSerializer.Serialize(payload);

        try
        {
            _context.BeginTransaction();
            var outbox = new OutboxMessage
            {
                ContextId = contextId,
                LastUpdated = insertedAt,
                OccurredAt = insertedAt,
                MessageBody = serializedPayload,
                MessageRegistryId = messageRegistryId,
                TraceParent = traceParent,
                TraceState = traceState
            };

            var messageId = await _context.OutboxMessageRepository.CreateOutboxMessage(outbox);
            if (filters != null)
            {
                var convertedFilters = filters.Select(f => new MessageFilter
                {
                    MessageId = messageId,
                    FilterKey = f.Key,
                    FilterValue = f.Value
                });
                await _context.FilterRepository.CreateFilters(convertedFilters);
            }

            _context.CommitTransaction();
            return messageId;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Caught exception when creating a message, rolling back");
            _context.RollBackTransaction();
            throw;
        }
    }
}
