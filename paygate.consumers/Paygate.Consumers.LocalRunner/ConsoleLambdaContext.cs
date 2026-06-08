using Amazon.Lambda.Core;

namespace Paygate.Consumers.LocalRunner;

/// <summary>
/// Minimal ILambdaContext for running Lambda handlers outside AWS. The handlers
/// only use Logger, so the rest is left at harmless defaults.
/// </summary>
public sealed class ConsoleLambdaContext : ILambdaContext
{
    public ConsoleLambdaContext(string functionName)
    {
        FunctionName = functionName;
        Logger = new ConsoleLambdaLogger(functionName);
    }

    public string AwsRequestId => Guid.NewGuid().ToString();
    public IClientContext ClientContext => null!;
    public string FunctionName { get; }
    public string FunctionVersion => "$LOCAL";
    public ICognitoIdentity Identity => null!;
    public string InvokedFunctionArn => $"arn:aws:lambda:local:000000000000:function:{FunctionName}";
    public ILambdaLogger Logger { get; }
    public string LogGroupName => "/local/paygate";
    public string LogStreamName => "local";
    public int MemoryLimitInMB => 256;
    public TimeSpan RemainingTime => TimeSpan.FromSeconds(30);
}

/// <summary>
/// Writes Lambda log lines to the console (tagged with the current trace id) and
/// forwards them to the Signals telemetry panel via <see cref="TelemetryForwarder"/>.
/// </summary>
public sealed class ConsoleLambdaLogger : ILambdaLogger
{
    private readonly string _service;

    public ConsoleLambdaLogger(string service) => _service = service;

    public void Log(string message) => Write("Information", message);

    public void LogLine(string message) => Write("Information", message);

    // ILambdaLogger's default LogInformation/LogWarning/LogError methods route to this
    // formatted overload. We MUST implement it — the default implementation would call
    // back into the interface and recurse until the stack overflows.
    public void Log(string level, string message, params object[] args)
        => Write(level, Render(message, args));

    private void Write(string level, string message)
    {
        var activity = System.Diagnostics.Activity.Current;
        var traceId = activity?.TraceId.ToString();
        var spanId  = activity?.SpanId.ToString();

        var prefix = traceId is null ? "" : $"[trace {traceId[..8]}] ";
        Console.WriteLine($"{prefix}{level,-11} {message}");

        TelemetryForwarder.Enqueue(_service, level, message, traceId, spanId);
    }

    // Render Serilog-style "{Name}" placeholders positionally from args (good enough
    // for console output; the structured names stay visible if there are no args).
    private static string Render(string template, object[] args)
    {
        if (args is null || args.Length == 0) return template;
        var i = 0;
        return System.Text.RegularExpressions.Regex.Replace(
            template, "{[^{}]+}",
            m => i < args.Length ? (args[i++]?.ToString() ?? "null") : m.Value);
    }
}
