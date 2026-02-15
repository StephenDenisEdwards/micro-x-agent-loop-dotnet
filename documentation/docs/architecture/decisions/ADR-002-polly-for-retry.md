# ADR-002: Polly for API Retry Resilience

## Status

Accepted

## Context

The Anthropic API enforces rate limits (e.g., 30,000 input tokens per minute on lower tiers). When exceeded, the API returns HTTP 429 (Too Many Requests). Without retry logic, the agent fails immediately and the user must manually re-submit their prompt.

Options considered:

1. **Manual retry loop** — simple `for` loop with `Thread.Sleep`, no dependencies
2. **Polly** — industry-standard .NET resilience library with built-in backoff strategies
3. **Microsoft.Extensions.Http.Resilience** — newer abstraction over Polly, requires DI container

## Decision

Use **Polly 8** (`ResiliencePipeline`) with exponential backoff for HTTP 429 retries. The pipeline is configured as a static singleton in `LlmClient` and wraps the streaming API call.

Configuration:
- Max retries: 5
- Backoff: exponential starting at 10 seconds (10s, 20s, 40s, 80s, 160s)
- Trigger: `HttpRequestException` with status code 429
- User feedback: retry attempts logged to stderr

## Consequences

**Easier:**
- Rate limit errors recover automatically without user intervention
- Polly's declarative API is readable and well-documented
- Can extend with circuit breaker, timeout, or other strategies later

**Harder:**
- Additional NuGet dependency
- Long waits on repeated rate limits (up to ~5 minutes total backoff)
- Retry wraps the entire streaming call; a partial stream followed by a 429 would restart from scratch
