using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Paygate.Api.Telemetry;

public sealed class TelemetryStore
{
    private const int HistoryCapacity = 5_000;

    private readonly ConcurrentQueue<LogEntry> _history = new();
    private readonly List<Channel<LogEntry>> _subscribers = new();
    private readonly object _lock = new();

    public void Add(LogEntry entry)
    {
        _history.Enqueue(entry);
        while (_history.Count > HistoryCapacity)
            _history.TryDequeue(out _);

        lock (_lock)
        {
            foreach (var ch in _subscribers)
                ch.Writer.TryWrite(entry);
        }
    }

    public IReadOnlyList<LogEntry> Query(string? service, string? level, string? traceId, int limit = 500)
    {
        IEnumerable<LogEntry> entries = _history;

        if (service is not null)
            entries = entries.Where(e => e.Service.Equals(service, StringComparison.OrdinalIgnoreCase));
        if (level is not null)
            entries = entries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        if (traceId is not null)
            entries = entries.Where(e => e.TraceId == traceId);

        return entries.TakeLast(Math.Clamp(limit, 1, 2_000)).ToList();
    }

    public async IAsyncEnumerable<LogEntry> StreamAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock (_lock)
            _subscribers.Add(channel);

        try
        {
            await foreach (var entry in channel.Reader.ReadAllAsync(ct))
                yield return entry;
        }
        finally
        {
            lock (_lock)
                _subscribers.Remove(channel);
        }
    }
}
