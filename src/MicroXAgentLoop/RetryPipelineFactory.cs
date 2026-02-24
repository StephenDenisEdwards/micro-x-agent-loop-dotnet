using Polly;
using Polly.Retry;
using Serilog;

namespace MicroXAgentLoop;

/// <summary>
/// Shared Polly retry pipeline factory.
/// Provides presets for Anthropic API calls and MCP tool calls.
/// </summary>
public static class RetryPipelineFactory
{
    private const int DefaultMaxRetries = 5;
    private static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(10);

    private const int McpMaxRetries = 2;
    private static readonly TimeSpan McpDelay = TimeSpan.FromSeconds(2);

    /// <summary>Default pipeline for Anthropic API calls (5 retries, 10s delay).</summary>
    public static ResiliencePipeline Create() =>
        Create(DefaultMaxRetries, DefaultDelay);

    /// <summary>Pipeline for MCP tool calls (2 retries, 2s delay, also handles TimeoutException).</summary>
    public static ResiliencePipeline CreateForMcp() =>
        Create(McpMaxRetries, McpDelay, typeof(TimeoutException));

    public static ResiliencePipeline Create(int maxRetries, TimeSpan delay, params Type[] additionalExceptions)
    {
        var predicateBuilder = new PredicateBuilder()
            .Handle<HttpRequestException>(ex =>
                ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .Handle<HttpRequestException>(ex => ex.StatusCode is null)
            .Handle<TaskCanceledException>(ex => ex.InnerException is TimeoutException);

        foreach (var exType in additionalExceptions)
            predicateBuilder.Handle<Exception>(ex => exType.IsInstanceOfType(ex));

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = delay,
                ShouldHandle = predicateBuilder,
                OnRetry = args =>
                {
                    var reason = args.Outcome.Exception switch
                    {
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "Rate limited",
                        HttpRequestException => "Connection error",
                        TaskCanceledException => "Request timed out",
                        TimeoutException => "Timeout",
                        _ => args.Outcome.Exception?.GetType().Name ?? "Unknown error",
                    };
                    Log.Warning(
                        "{Reason}. Retrying in {Delay}s (attempt {Attempt}/{Max})...",
                        reason, args.RetryDelay.TotalSeconds, args.AttemptNumber + 1, maxRetries);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }
}
