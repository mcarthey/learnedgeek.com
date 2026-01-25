You're building a .NET MAUI Blazor Hybrid app. Everything works great until you add error handling. Then you see this cryptic exception:

```
System.InvalidOperationException: 'WebViewNavigationManager' has not been initialized.
```

This happens when you try to access `NavigationManager.Uri` during early error handling—specifically inside an `ErrorBoundary.OnErrorAsync()` callback.

## Why It Happens

In MAUI Blazor Hybrid apps, the `NavigationManager` is actually a `WebViewNavigationManager` that wraps the native WebView. Unlike regular Blazor Server or WASM apps, the NavigationManager needs the WebView to be fully initialized before it can report the current URL.

The timing issue:
1. Your app starts loading
2. An error occurs early in the component lifecycle
3. Your `ErrorBoundary` catches it and calls `OnErrorAsync()`
4. Your logging service tries to capture the current URL via `NavigationManager.Uri`
5. **BOOM** — The WebView isn't ready yet

This is especially common in error boundaries because they're designed to catch problems during initialization—exactly when the NavigationManager might not be ready.

## The Fix

Wrap your `NavigationManager.Uri` access in a try-catch:

```csharp
public class LoggingService : ILoggingService
{
    private readonly NavigationManager _navigationManager;

    public LoggingService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public async Task<string> LogErrorAsync(string message, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Message = message,
            Url = GetCurrentUrlSafely(),
            Timestamp = DateTime.UtcNow
        };

        // ... rest of logging logic
        return entry.ReferenceId;
    }

    private string? GetCurrentUrlSafely()
    {
        try
        {
            return _navigationManager.Uri;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"NavigationManager not available: {ex.Message}");
            return null;
        }
    }
}
```

## Why Not Just Check for Null?

You might think "I'll just check if `NavigationManager` is null first." That won't work—the `NavigationManager` instance exists, it's just not *initialized*. The `Uri` property getter calls `EnsureInitialized()` internally, which throws if the WebView isn't ready.

There's no public property to check initialization status, so the try-catch is the cleanest solution.

## Testing the Fix

You can't easily mock `NavigationManager.Uri` because it's not virtual (more on that in a related post). Instead, create concrete test implementations:

```csharp
/// <summary>
/// Test NavigationManager that simulates an uninitialized state.
/// </summary>
private class UninitializedNavigationManager : NavigationManager
{
    public UninitializedNavigationManager()
    {
        // Don't call Initialize - leave in uninitialized state
    }

    protected override void EnsureInitialized()
    {
        throw new InvalidOperationException(
            "'WebViewNavigationManager' has not been initialized.");
    }
}

/// <summary>
/// Test NavigationManager with a configured URL.
/// </summary>
private class TestNavigationManager : NavigationManager
{
    public TestNavigationManager(string uri)
    {
        Initialize(uri, uri);
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        // No-op for tests
    }
}
```

Now your tests can verify both scenarios:

```csharp
[Fact]
public void GetCurrentUrlSafely_WhenUninitialized_ReturnsNull()
{
    var navManager = new UninitializedNavigationManager();
    var service = new LoggingService(navManager);

    var url = service.GetCurrentUrlSafely();

    Assert.Null(url);
}

[Fact]
public void GetCurrentUrlSafely_WhenInitialized_ReturnsUrl()
{
    var navManager = new TestNavigationManager("https://app.example.com/page");
    var service = new LoggingService(navManager);

    var url = service.GetCurrentUrlSafely();

    Assert.Equal("https://app.example.com/page", url);
}
```

## When You'll Hit This

- Error boundaries capturing startup exceptions
- Logging services that enrich logs with the current URL
- Analytics tracking page views during app initialization
- Any code that runs before the Blazor component tree is fully mounted

The pattern applies anywhere you access `NavigationManager` in MAUI Hybrid code that might run before the WebView is ready.

## Key Takeaways

1. **MAUI Blazor Hybrid has timing quirks** — The WebView-based NavigationManager isn't always ready when you expect
2. **Error boundaries are high-risk** — They catch errors during initialization, when services might not be ready
3. **Defensive programming saves the day** — A simple try-catch prevents cascading failures
4. **Test the failure mode** — Don't just test the happy path; verify your error handling handles its own errors

---

*This bug worked perfectly in the browser (Blazor WASM) but crashed on Android (MAUI Hybrid) because of this timing difference. Platform differences like this are why defensive coding matters.*

## Related Posts

- [Error Boundaries That Don't Trap Users](/blog/error-boundaries-that-dont-trap-users) — Building Blazor error boundaries that let users escape
- [Testing Non-Virtual Members Without Moq](/blog/testing-non-virtual-members-without-moq) — How to test NavigationManager behavior when you can't mock it directly
- [Tracer Bullet Development: Prove Your Pipeline](/blog/tracer-bullet-development-prove-your-pipeline) — How error logging became the first proof of the entire architecture
