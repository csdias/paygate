using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paygate.Api.Telemetry;

public static class TelemetryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/telemetry").WithTags("Telemetry");

        // External services (OutboxPublisher, Lambda) push entries here
        group.MapPost("/ingest", (LogEntry[] entries, TelemetryStore store) =>
        {
            foreach (var entry in entries)
                store.Add(entry);
            return Results.NoContent();
        }).WithName("IngestLogs");

        // React fetches historical entries with optional filters
        group.MapGet("/logs", (
            TelemetryStore store,
            string? service,
            string? level,
            string? traceId,
            int limit = 500) =>
            Results.Ok(store.Query(service, level, traceId, limit)))
            .WithName("QueryLogs");

        // React connects here for live push via Server-Sent Events
        group.MapGet("/stream", async (TelemetryStore store, HttpContext ctx) =>
        {
            var ct = ctx.RequestAborted;
            ctx.Response.Headers["Content-Type"]      = "text/event-stream";
            ctx.Response.Headers["Cache-Control"]     = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                await foreach (var entry in store.StreamAsync(ct))
                {
                    var json = JsonSerializer.Serialize(entry, JsonOptions);
                    await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // client disconnected — normal exit
            }
        }).WithName("StreamLogs");

        return app;
    }
}
