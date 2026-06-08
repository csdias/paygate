using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Paygate.Api.Telemetry;

public sealed class TelemetrySink : ILogEventSink
{
    private static readonly HashSet<string> ExcludedProperties =
        ["SourceContext", "SpanId", "TraceId", "ParentId", "RequestId", "RequestPath", "ConnectionId"];

    private readonly TelemetryStore _store;
    private readonly string _service;

    public TelemetrySink(TelemetryStore store, string service)
    {
        _store = store;
        _service = service;
    }

    public void Emit(LogEvent logEvent)
    {
        var activity = Activity.Current;
        var traceId = activity?.Id is not null ? activity.TraceId.ToString() : null;
        var spanId  = activity?.Id is not null ? activity.SpanId.ToString()  : null;

        var props = logEvent.Properties
            .Where(p => !ExcludedProperties.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value.ToString().Trim('"'));

        _store.Add(new LogEntry(
            Timestamp:  logEvent.Timestamp,
            Level:      logEvent.Level.ToString(),
            Message:    logEvent.RenderMessage(),
            Service:    _service,
            TraceId:    traceId,
            SpanId:     spanId,
            Properties: props));
    }
}
