using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Pay.Message.Exchange.OutboxPublisher.Entities;
using Pay.Message.Exchange.OutboxPublisher.Sns.Entities;

namespace Pay.Message.Exchange.OutboxPublisher.Sns;

public class SnsMessagePublisher : IMessagePublisher
{
    private readonly IAmazonSimpleNotificationService _sns;
    private readonly IMapper _mapper;
    private readonly ILogger<SnsMessagePublisher> _logger;

    public SnsMessagePublisher(IAmazonSimpleNotificationService sns, IMapper mapper,
        ILogger<SnsMessagePublisher> logger)
    {
        _sns = sns;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<bool> PublishMessages(OutboxMessage entry)
    {
        var attributes = entry.MessageFilters.ToDictionary(
            x => x.FilterKey,
            y => new MessageAttributeValue { StringValue = y.FilterValue, DataType = nameof(String) });

        // Propagate W3C trace context as standard SNS message attributes so any
        // consumer can read them from the notification envelope without parsing the body
        if (!string.IsNullOrEmpty(entry.TraceParent))
            attributes["traceparent"] = new MessageAttributeValue { StringValue = entry.TraceParent, DataType = "String" };
        if (!string.IsNullOrEmpty(entry.TraceState))
            attributes["tracestate"]  = new MessageAttributeValue { StringValue = entry.TraceState,  DataType = "String" };

        var request = new PublishRequest
        {
            TopicArn          = entry.MessageRegistry.Topic.Trim(),
            Message           = JsonSerializer.Serialize(_mapper.Map<SnsMessage>(entry)),
            MessageAttributes = attributes
        };

        try
        {
            var response = await _sns.PublishAsync(request);
            return !string.IsNullOrEmpty(response.MessageId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to send message id: {MessageId}", entry.MessageId);
            return false;
        }
    }
}
