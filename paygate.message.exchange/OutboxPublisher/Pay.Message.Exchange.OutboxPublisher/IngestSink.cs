using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Serilog.Core;
using Serilog.Events;

namespace Pay.Message.Exchange.OutboxPublisher;

/// <summary>
/// A Serilog sink that forwards log events to the API's /telemetry/ingest endpoint
/// so OutboxPublisher logs appear in the paygate.admin Signals panel, correlated with
/// the API and consumers by trace id.
///
/// Best-effort and batched: events are queued and a background loop POSTs them; any
/// HTTP failure is swallowed. Only added to the pipeline when TELEMETRY_INGEST_URL is
/// set (see Program.ConfigureSerilog), so production logging is unaffected.
/// </summary>
public sealed class IngestSink : ILogEventSink, IDisposable
{
    private static readonly HashSet<string> ExcludedProperties =
        ["SourceContext", "SpanId", "TraceId", "ParentId", "RequestId", "RequestPath", "ConnectionId"];

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _ingestUrl;
    private readonly string _service;
    private readonly ConcurrentQueue<object> _queue = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly CancellationTokenSource _cts = new();

    public IngestSink(string ingestUrl, string service)
    {
        _ingestUrl = ingestUrl;
        _service = service;
        _ = Task.Run(FlushLoopAsync);
    }

    public void Emit(LogEvent logEvent)
    {
        var activity = Activity.Current;
        var props = logEvent.Properties
            .Where(p => !ExcludedProperties.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value.ToString().Trim('"'));

        _queue.Enqueue(new
        {
            timestamp = logEvent.Timestamp,
            level = logEvent.Level.ToString(),
            message = logEvent.RenderMessage(),
            service = _service,
            traceId = activity?.Id is not null ? activity.TraceId.ToString() : null,
            spanId  = activity?.Id is not null ? activity.SpanId.ToString()  : null,
            properties = props
        });
    }

    private async Task FlushLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try { await Task.Delay(500, _cts.Token); } catch (OperationCanceledException) { break; }
            await FlushAsync();
        }
        await FlushAsync();
    }

    private async Task FlushAsync()
    {
        if (_queue.IsEmpty) return;

        var batch = new List<object>();
        while (_queue.TryDequeue(out var entry)) batch.Add(entry);
        if (batch.Count == 0) return;

        try
        {
            var body = new StringContent(JsonSerializer.Serialize(batch, Json), Encoding.UTF8, "application/json");
            await _http.PostAsync(_ingestUrl, body);
        }
        catch
        {
            // API unreachable — drop this batch, never let logging break publishing.
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        FlushAsync().GetAwaiter().GetResult();
        _http.Dispose();
        _cts.Dispose();
    }
}
