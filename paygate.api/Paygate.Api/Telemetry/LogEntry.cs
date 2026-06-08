namespace Paygate.Api.Telemetry;

public record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string Service,
    string? TraceId,
    string? SpanId,
    IReadOnlyDictionary<string, string> Properties
);
