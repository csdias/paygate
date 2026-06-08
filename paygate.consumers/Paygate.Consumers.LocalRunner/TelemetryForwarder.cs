using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Paygate.Consumers.LocalRunner;

/// <summary>
/// Forwards log entries to the API's /telemetry/ingest endpoint so consumer logs
/// show up in the paygate.admin Signals panel alongside the API and publisher.
///
/// Best-effort: entries are queued and flushed by a background loop; any HTTP
/// failure is swallowed so logging never affects message processing. Disabled
/// (a no-op) unless TELEMETRY_INGEST_URL is set.
/// </summary>
public static class TelemetryForwarder
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly ConcurrentQueue<object> Queue = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private static string? _ingestUrl;

    public static void Start(CancellationToken ct)
    {
        _ingestUrl = Environment.GetEnvironmentVariable("TELEMETRY_INGEST_URL");
        if (string.IsNullOrWhiteSpace(_ingestUrl)) return; // forwarding disabled

        _ = Task.Run(() => FlushLoopAsync(ct), ct);
    }

    public static void Enqueue(string service, string level, string message, string? traceId, string? spanId)
    {
        if (_ingestUrl is null) return;
        Queue.Enqueue(new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            message,
            service,
            traceId,
            spanId,
            properties = new Dictionary<string, string>()
        });
    }

    private static async Task FlushLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(500, ct); } catch (OperationCanceledException) { break; }
            await FlushAsync();
        }
        await FlushAsync(); // final drain on shutdown
    }

    private static async Task FlushAsync()
    {
        if (Queue.IsEmpty || _ingestUrl is null) return;

        var batch = new List<object>();
        while (Queue.TryDequeue(out var entry)) batch.Add(entry);
        if (batch.Count == 0) return;

        try
        {
            var body = new StringContent(JsonSerializer.Serialize(batch, Json), Encoding.UTF8, "application/json");
            await Http.PostAsync(_ingestUrl, body);
        }
        catch
        {
            // API not running / unreachable — drop this batch, keep processing messages.
        }
    }
}
