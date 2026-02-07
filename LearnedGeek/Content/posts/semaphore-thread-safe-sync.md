# Thread-Safe Synchronization with SemaphoreSlim

I had a sync bug that only happened on Mondays.

The app would resume from background, check for pending items, and start a sync. Simultaneously, the network recovery handler would detect connectivity and start a sync. Two syncs running at once. Data corruption. Angry phone calls.

The fix looked embarrassingly simple in retrospect.

## The Pattern That Looks Safe But Isn't

This code exists in almost every mobile app I've seen:

```csharp
private bool _isSyncing;

public async Task SyncAsync()
{
    if (_isSyncing) return;  // Check
    _isSyncing = true;        // Set

    try
    {
        await DoSyncAsync();
    }
    finally
    {
        _isSyncing = false;   // Reset
    }
}
```

Looks reasonable, right? Check if we're already syncing, set the flag, do the work, clear the flag.

Here's the problem. Between the check and the set, another thread can sneak in:

```
Thread A: if (_isSyncing) → false
Thread B: if (_isSyncing) → false  ← Both pass!
Thread A: _isSyncing = true
Thread B: _isSyncing = true
Thread A: await DoSyncAsync()
Thread B: await DoSyncAsync()      ← Both execute!
```

This is a classic race condition. And it's everywhere in mobile apps.

## Why It Happens More Than You'd Think

Mobile apps are full of concurrent triggers:

- **App resume**: Multiple components check "should I sync?" simultaneously
- **Network recovery**: Connectivity change fires from multiple listeners
- **Timer callbacks**: Background timers fire while user taps manual sync
- **Fire-and-forget**: `_ = SyncAsync()` creates overlapping execution

In my case, app resume and network recovery both fired within milliseconds of each other. Most of the time, the timing was fine. On Mondays, for some reason (maybe server load patterns?), the race condition hit consistently.

## The Fix: SemaphoreSlim

`SemaphoreSlim` provides thread-safe "one at a time" semantics that work with async/await:

```csharp
private readonly SemaphoreSlim _syncLock = new(1, 1);  // Initial: 1, Max: 1

public async Task<bool> SyncAsync()
{
    // Try to acquire lock without waiting
    if (!await _syncLock.WaitAsync(0))
    {
        // Another sync is in progress
        return false;
    }

    try
    {
        return await DoSyncAsync();
    }
    finally
    {
        _syncLock.Release();
    }
}
```

The `WaitAsync(0)` is the key. It means "try to acquire the lock, but don't wait." If the lock is taken, return immediately with `false`. No blocking, no waiting, just "sorry, someone else is syncing."

## How It Actually Works

```
Thread A: WaitAsync(0) → acquires (count: 1 → 0)
Thread B: WaitAsync(0) → fails (count is 0)  ✓ Blocked!
Thread A: DoSyncAsync()
Thread A: Release() → (count: 0 → 1)
Thread C: WaitAsync(0) → acquires (count: 1 → 0)
```

The `new(1, 1)` creates a semaphore with one permit. When someone takes it, the count drops to zero. Anyone else who tries gets rejected. When the holder releases, the count goes back to one.

## The Full Implementation

```csharp
public class SyncService : ISyncService, IDisposable
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isDisposed;

    public async Task<bool> SyncAsync()
    {
        if (!await _syncLock.WaitAsync(0))
        {
            return false;  // Already syncing
        }

        try
        {
            if (!_syncStatus.IsOnline)
            {
                _syncStatus.SetStatus(SyncStatus.Offline);
                return false;
            }

            _syncStatus.SetStatus(SyncStatus.Syncing);
            return await DoSyncAsync();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _syncLock.Dispose();
        _isDisposed = true;
    }
}
```

## Callers Don't Need to Change

The beautiful part: existing code continues to work:

```csharp
// On app resume - fire and forget
_ = SyncAsync();

// On network recovery - fire and forget
_ = SyncAsync();

// Manual trigger - now with feedback
var success = await SyncAsync();
if (!success)
{
    ShowToast("Sync already in progress");
}
```

Multiple callers can all call `SyncAsync()` simultaneously. Only one will actually run. The others get a clean `false` return.

## Why Not `lock`?

The `lock` statement works for synchronous code:

```csharp
lock (_syncLock)
{
    // Protected code
}
```

But you can't `await` inside a `lock` block. The compiler won't let you. That's because `lock` holds a thread-level mutex, and awaited code might resume on a different thread.

`SemaphoreSlim` is designed for async. It doesn't care which thread releases it.

## Common Mistakes

**Forgetting to release:**

```csharp
// WRONG
await _syncLock.WaitAsync();
if (!_isOnline) return;  // Lock never released!
_syncLock.Release();
```

Always use try-finally.

**Wrong initial count:**

```csharp
// WRONG - starts locked, nothing can enter
private readonly SemaphoreSlim _lock = new(0, 1);

// CORRECT - one permit available
private readonly SemaphoreSlim _lock = new(1, 1);
```

**Disposing while in use:**

Track disposal state and cancel pending operations before disposing the semaphore.

## The Monday Bug, Solved

After switching to `SemaphoreSlim`, the Monday bug disappeared. More importantly, I could now *prove* it was fixed. The pattern is deterministic—only one sync runs at a time, guaranteed by the runtime, not by luck.

Boolean flags for synchronization are a race condition waiting to happen. `SemaphoreSlim` is simple, safe, and async-compatible. Use it.

---

*Part of the Production Hardening series. See also: [HTTP Resilience with Polly](/Blog/Post/polly-http-resilience-patterns) for retry and circuit breaker patterns that work with thread-safe sync.*
