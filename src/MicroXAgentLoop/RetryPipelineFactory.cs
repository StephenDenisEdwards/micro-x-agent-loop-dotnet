using Polly;
using Polly.Retry;
using Serilog;

namespace MicroXAgentLoop;

/// <summary>
/// Shared Polly retry pipeline for Anthropic API calls.
/// Handles rate limits (429), connection errors, and timeouts.
/// </summary>
public static class RetryPipelineFactory
{
    private const int MaxRetryAttempts = 5;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);

    public static ResiliencePipeline Create() =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = InitialDelay,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex =>
                        ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    .Handle<HttpRequestException>(ex => ex.StatusCode is null)
                    .Handle<TaskCanceledException>(),
                OnRetry = args =>
                {
                    var reason = args.Outcome.Exception switch
                    {
                        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "Rate limited",
                        HttpRequestException => "Connection error",
                        TaskCanceledException => "Request timed out",
                        _ => args.Outcome.Exception?.GetType().Name ?? "Unknown error",
                    };
                    Log.Warning(
                        "{Reason}. Retrying in {Delay}s (attempt {Attempt}/{Max})...",
                        reason, args.RetryDelay.TotalSeconds, args.AttemptNumber + 1, MaxRetryAttempts);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
}
