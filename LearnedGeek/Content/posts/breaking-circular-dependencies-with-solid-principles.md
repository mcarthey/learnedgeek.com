The build failed. Circular dependency detected.

I stared at the error message, already knowing what I'd find. Two services that needed each other, a classic architectural tangle. My first instinct was to reach for `IServiceProvider` and lazy-load one of them. It would compile. It would run. I could move on with my day.

But I've been burned by that shortcut before. The quick fix today becomes tomorrow's debugging nightmare. So I took the harder path—and I want to show you why you should too.

## The Problem: Two Services That Need Each Other

my project's mobile app has two key services:

- **SyncService**: Synchronizes work orders with the server, logs its operations
- **LoggingService**: Logs errors to local storage, syncs them to the server

Here's the dependency chain that emerged:

```
SyncService → ILoggingService (to log sync operations)
LoggingService → ISyncService (to update "Last Sync" timestamp when logs sync)
```

Classic circular dependency. The app wouldn't even compile.

## The Easy Fix (Don't Do This)

The fastest solution? Lazy loading via `IServiceProvider`:

```csharp
public class LoggingService : ILoggingService
{
    private readonly IServiceProvider _serviceProvider;
    private ISyncService? _syncService;

    // Lazy load to "avoid" circular dependency
    private ISyncService? SyncService =>
        _syncService ??= _serviceProvider.GetService<ISyncService>();

    public async Task<bool> SyncToServerAsync()
    {
        // ... sync logs ...

        // Notify that sync happened
        SyncService?.NotifySyncCompleted();
        return true;
    }
}
```

This compiles. It runs. Ship it, right?

**Wrong.**

## Why Lazy Loading Is a Code Smell

This pattern has several problems:

1. **Hidden Dependencies**: The constructor no longer tells the truth about what this class needs. Dependencies are discovered at runtime, not compile time.

2. **Testability Nightmare**: Unit tests now need a full `IServiceProvider` mock instead of simple constructor injection.

3. **Violation of Single Responsibility**: LoggingService now has two jobs—logging AND knowing how to navigate the DI container.

4. **The Underlying Problem Remains**: You haven't fixed the architecture; you've hidden the symptom. Every new service that needs this shared state will face the same issue.

5. **Temporal Coupling**: The code assumes `ISyncService` will be registered and available when needed. That's an implicit contract that can break silently.

As I tell my students at WCTC: **if you need `IServiceProvider` in a business service, your architecture is asking for help.**

## The Real Solution: Extract the Shared Concern

The circular dependency exists because both services need the same piece of state: sync status information. The "Last Sync" timestamp, online/offline status, and pending count are cross-cutting concerns that don't belong to either service exclusively.

The fix is to extract this shared state into its own service:

```csharp
public interface ISyncStatusService
{
    SyncStatus Status { get; }
    DateTime? LastSyncTime { get; }
    int PendingEventCount { get; }
    bool IsOnline { get; }

    void SetStatus(SyncStatus status, string? message = null);
    void NotifySyncCompleted(string? source = null);
    void SetPendingCount(int count);

    event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;
}
```

Now the dependency graph becomes:

```
ISyncStatusService (owns status, no dependencies on business services)
       ↑                    ↑
       |                    |
  SyncService         LoggingService
```

No cycles. Clean dependencies. Each service depends on the abstraction, not on each other.

## The Implementation

The `SyncStatusService` is intentionally simple—it owns state and fires events:

```csharp
public class SyncStatusService : ISyncStatusService, IDisposable
{
    private SyncStatus _status = SyncStatus.Idle;
    private DateTime? _lastSyncTime;
    private int _pendingEventCount;

    public SyncStatusService()
    {
        // Handle connectivity changes
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public void NotifySyncCompleted(string? source = null)
    {
        _lastSyncTime = DateTime.UtcNow;

        // Fire event so UI updates
        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            OldStatus = _status,
            NewStatus = _status,
            Message = $"{source} sync completed"
        });
    }

    // ... rest of implementation
}
```

Now both services can report sync activity without knowing about each other:

```csharp
// In SyncService
_syncStatus.NotifySyncCompleted("WorkOrders");

// In LoggingService
_syncStatus.NotifySyncCompleted("Logs");
```

The UI subscribes to `ISyncStatusService.StatusChanged` and gets updates from both sources.

## SOLID Principles in Action

This refactoring demonstrates several SOLID principles:

### Single Responsibility Principle (SRP)
Each service now has one reason to change:
- `SyncStatusService`: How we track and report sync status
- `SyncService`: How we synchronize work orders
- `LoggingService`: How we log and sync error data

### Open/Closed Principle (OCP)
Adding a new service that syncs data (say, `PhotoSyncService`) requires zero changes to existing services. Just inject `ISyncStatusService` and call `NotifySyncCompleted("Photos")`.

### Dependency Inversion Principle (DIP)
High-level modules (`SyncService`, `LoggingService`) depend on the abstraction (`ISyncStatusService`), not on each other. The abstraction doesn't depend on details.

### Interface Segregation Principle (ISP)
`ISyncStatusService` exposes only what's needed for status tracking. It doesn't include work order sync methods or logging methods—those belong to their respective interfaces.

## The Payoff

This refactoring took about 30 minutes longer than the lazy loading hack. Here's what that investment bought:

1. **Clear Dependencies**: Constructors tell the complete truth
2. **Easy Testing**: Mock `ISyncStatusService` directly, no service locator needed
3. **Extensibility**: New sync sources integrate without touching existing code
4. **Debuggability**: Status changes have a clear source (`"Logs"`, `"WorkOrders"`)
5. **No Hidden Runtime Failures**: All dependencies resolved at startup

## The Lesson

When you encounter a circular dependency, resist the urge to patch it. The cycle is your code telling you that responsibilities are misallocated. Listen to it.

Extract the shared concern. Create a new abstraction. Let the dependency graph guide you toward better architecture.

The lazy fix saves 30 minutes today and costs hours in maintenance, testing, and debugging tomorrow. The architectural fix is an investment that pays dividends every time you extend the system.

---

*Circular dependencies are your code asking for help. Listen to it, and let the dependency graph guide you toward better architecture.*
