# HTTP Resilience with Polly: Retry and Circuit Breaker Patterns

The mobile app worked great on WiFi. Then a user drove through a tunnel.

The sync button showed "syncing" for 30 seconds. Then an error. They tapped it again. Another 30 seconds. More errors. By the time they emerged from the tunnel, they'd queued up a dozen retry attempts that all fired simultaneously.

This is what happens when you treat HTTP calls as reliable operations. They're not.

## The Harsh Reality of Mobile Networks

Mobile apps face a world of:

- **Network flakiness**: Cell signals drop, WiFi ranges end, subways go underground
- **Server hiccups**: Deployments cause brief outages, load spikes cause timeouts
- **Cascade failures**: One slow dependency backs up requests until everything fails

Without resilience patterns, temporary failures crash the user experience. With them, most failures become invisible retries that users never notice.

## Enter Polly

[Polly](https://github.com/App-vNext/Polly) is a .NET resilience library that wraps your HTTP calls with automatic retry logic and circuit breakers. Instead of writing defensive code everywhere, you configure policies once and let the library handle the rest.

The two patterns that matter most:

**Retry**: Automatically retry failed operations with exponential backoff
**Circuit Breaker**: Stop calling a failing service to let it recover

## The Setup

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Http.Polly" Version="10.0.0" />
</ItemGroup>
```

In your `MauiProgram.cs` (or `Program.cs` for web APIs):

```csharp
using Polly;
using Polly.Extensions.Http;

// Define retry policy
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()  // 5xx, 408, network errors
    .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)) +
            TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100)),
        onRetry: (outcome, timespan, retryAttempt, _) =>
        {
            Debug.WriteLine($"[Polly] Retry {retryAttempt} after {timespan.TotalSeconds:F1}s");
        });

// Define circuit breaker policy
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (_, breakDelay) =>
            Debug.WriteLine($"[Polly] Circuit OPEN for {breakDelay.TotalSeconds}s"),
        onReset: () => Debug.WriteLine("[Polly] Circuit CLOSED"));

// Register HttpClient with policies
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(retryPolicy)
.AddPolicyHandler(circuitBreakerPolicy);
```

## Exponential Backoff with Jitter

That `sleepDurationProvider` creates delays of roughly:
- Attempt 1: ~1 second
- Attempt 2: ~2 seconds
- Attempt 3: ~4 seconds

The random jitter (0-100ms) prevents the "thundering herd" problem where a thousand clients retry simultaneously and DDoS your server.

Why not linear backoff (1s, 2s, 3s)? Because if the server is overloaded, giving it slightly more time each retry actually helps. Exponential backoff respects recovery time.

## Circuit Breaker: The Kill Switch

The circuit breaker has three states:

**CLOSED** (normal): Requests flow through. Failures are counted.

**OPEN** (fail-fast): After 5 consecutive failures, the circuit "opens." All requests fail immediately with `BrokenCircuitException`. No actual HTTP calls are made. This gives the server time to recover.

**HALF-OPEN** (testing): After 30 seconds, one request is allowed through. If it succeeds, the circuit closes. If it fails, it opens again.

Without a circuit breaker, a dead server means every request hangs for 30 seconds. Users see frozen UI. Request queues back up. With a circuit breaker, users see instant "offline" feedback and can keep working.

## Handling the Open Circuit in Your Code

```csharp
public async Task<WorkOrder?> GetWorkOrderAsync(Guid id)
{
    try
    {
        return await _httpClient.GetFromJsonAsync<WorkOrder>($"/api/workorders/{id}");
    }
    catch (BrokenCircuitException)
    {
        // Circuit is open - show offline state
        _syncStatus.SetStatus(SyncStatus.Offline);
        return await _localDb.GetWorkOrderAsync(id);  // Return cached data
    }
}
```

When Polly tells you the circuit is open, don't fight it. Fall back to cached data and let the server recover.

## Policy Order Matters

```csharp
.AddPolicyHandler(retryPolicy)          // Outer
.AddPolicyHandler(circuitBreakerPolicy) // Inner
```

This means:
1. Request is made
2. If circuit is open, fail immediately (no retry)
3. If circuit is closed and request fails, circuit records the failure
4. Retry policy waits and tries again
5. If retries exhausted, failure propagates up

The retry wraps the circuit breaker, not the other way around.

## The Settings That Work for Mobile

```csharp
retryCount: 3,                              // Don't exhaust battery
sleepDurationProvider: exponential,         // Respect server recovery
handledEventsAllowedBeforeBreaking: 5,      // Detect real outages
durationOfBreak: TimeSpan.FromSeconds(30),  // Give server time
```

For backend-to-backend services, you might use more retries and longer breaks. For real-time features where users need instant feedback, fewer retries and shorter waits.

## The Idempotency Gotcha

Retrying a POST that creates a resource may create duplicates. If your sync endpoint isn't idempotent, add idempotency keys:

```csharp
var request = new HttpRequestMessage(HttpMethod.Post, "/api/sync");
request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
```

The server should track these keys and return the cached response for duplicates.

## What We Learned

After adding Polly to our mobile app:

- **Network hiccups became invisible**. Users didn't notice most transient failures.
- **Server deployments stopped breaking mobile clients**. The circuit opened during deploys, users saw "offline" briefly, then everything recovered.
- **Debug logs told a story**. The `onRetry` and `onBreak` callbacks showed exactly what was happening in production.

Polly transforms brittle HTTP calls into resilient operations. For mobile apps especially, these patterns are essential. Users don't care *why* the network failed—they just want the app to handle it gracefully.

---

*Part of the Production Hardening series. See also: [Thread-Safe Synchronization with SemaphoreSlim](/blog/semaphore-thread-safe-sync) for protecting your sync operations from race conditions.*
