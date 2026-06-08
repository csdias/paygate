using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pay.Message.Exchange.OutboxPublisher.Services;

namespace Pay.Message.Exchange.OutboxPublisher;

public class OutboxPublisherHostedService : IHostedService
{
    private readonly ILogger<OutboxPublisherHostedService> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IMessageProcessor _messageProcessor;

    private CancellationTokenSource _cts;
    private Task _processMessagesTask;

    public OutboxPublisherHostedService(IMessageProcessor messageProcessor,
        IHostApplicationLifetime applicationLifetime, ILogger<OutboxPublisherHostedService> logger)
    {
        _messageProcessor = messageProcessor;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Starting to process outbox");
        _processMessagesTask = Task.Run(async () =>
        {
            try
            {
                await _messageProcessor.ProcessMessagesAsync(_cts.Token);
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Failure in ProcessMessagesAsync, shutting down");
            }
            finally
            {
                _applicationLifetime.StopApplication();
            }
        }, _cts.Token);

        if (cancellationToken.IsCancellationRequested)
            _cts.Cancel();

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _cts.Cancel();
            await _processMessagesTask;
        }
        finally
        {
            _logger.LogInformation("Finished processing outbox");
            _applicationLifetime.StopApplication();
        }
    }
}
