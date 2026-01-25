You're writing a unit test. You try to mock `NavigationManager.Uri` with Moq:

```csharp
var mockNav = new Mock<NavigationManager>();
mockNav.Setup(x => x.Uri).Returns("https://example.com/test");
```

And Moq throws this at you:

```
System.NotSupportedException: Non-overridable members
(here: NavigationManager.get_Uri) may not be used in
setup / verification expressions.
```

What's happening? And why does this connect to SOLID principles you learned in school?

## Why Moq Can't Mock It

Moq works by creating a **dynamic proxy**—a subclass of your type that overrides the methods you want to mock. The key word is "overrides."

In C#, you can only override members that are `virtual`, `abstract`, or defined in an interface. `NavigationManager.Uri` is none of these:

```csharp
public abstract class NavigationManager
{
    // NOT virtual - can't be overridden!
    public string Uri => ...;
}
```

Microsoft designed it this way because `Uri` calls internal initialization logic (`EnsureInitialized()`). Making it virtual would let subclasses bypass that safety check.

## The SOLID Connection

This is a real-world example of the **Liskov Substitution Principle (LSP)** in action.

LSP says: *"Objects of a superclass should be replaceable with objects of a subclass without breaking the application."*

Microsoft is essentially saying: "We don't trust subclasses to implement `Uri` correctly, so we're not letting you override it." That's a reasonable design choice for framework code, but it creates testing friction.

It also touches on the **Dependency Inversion Principle (DIP)**: *"Depend on abstractions, not concretions."*

If `NavigationManager` were an interface (`INavigationManager`), you could mock it easily. But it's an abstract class with non-virtual members—a hybrid that's harder to test.

## Solution 1: Create Test Implementations

Since you can't override `Uri` directly, override the methods it calls internally:

```csharp
/// <summary>
/// Test NavigationManager that simulates an uninitialized state.
/// </summary>
private class UninitializedNavigationManager : NavigationManager
{
    protected override void EnsureInitialized()
    {
        throw new InvalidOperationException(
            "'WebViewNavigationManager' has not been initialized.");
    }
}

/// <summary>
/// Test NavigationManager with a working URL.
/// </summary>
private class TestNavigationManager : NavigationManager
{
    public TestNavigationManager(string uri)
    {
        Initialize(uri, uri);
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        // Required abstract method - no-op for tests
    }
}
```

Now you can test both scenarios:

```csharp
[Fact]
public void WhenNavigationManagerThrows_ReturnsNull()
{
    var navManager = new UninitializedNavigationManager();
    var service = new LoggingService(navManager);

    var result = service.GetCurrentUrlSafely();

    Assert.Null(result);
}

[Fact]
public void WhenNavigationManagerWorks_ReturnsUrl()
{
    var navManager = new TestNavigationManager("https://example.com/page");
    var service = new LoggingService(navManager);

    var result = service.GetCurrentUrlSafely();

    Assert.Equal("https://example.com/page", result);
}
```

## Solution 2: Wrap It (Composition over Inheritance)

If you control the code that uses `NavigationManager`, you can apply DIP yourself by introducing an abstraction:

```csharp
public interface INavigationService
{
    string? CurrentUrl { get; }
    void NavigateTo(string url);
}

public class BlazorNavigationService : INavigationService
{
    private readonly NavigationManager _navigationManager;

    public BlazorNavigationService(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public string? CurrentUrl
    {
        get
        {
            try { return _navigationManager.Uri; }
            catch { return null; }
        }
    }

    public void NavigateTo(string url)
    {
        _navigationManager.NavigateTo(url);
    }
}
```

Now your code depends on `INavigationService`, which is trivially mockable:

```csharp
var mockNav = new Mock<INavigationService>();
mockNav.Setup(x => x.CurrentUrl).Returns("https://example.com/test");
```

## When to Use Which Approach

| Approach | Best For |
|----------|----------|
| **Test implementations** | Testing framework behavior, testing failure modes |
| **Wrapper interface** | Isolating your code from framework details |

The test implementation approach is great when you need to verify how your code handles *specific framework behaviors*—like "what happens when NavigationManager throws?"

The wrapper approach is better for *general isolation*—your code doesn't even know NavigationManager exists.

## The Bigger Lesson

This situation teaches several important concepts:

1. **Virtual isn't just syntax** — It's a design decision about extensibility
2. **Framework constraints affect testability** — Sometimes you have to work around them
3. **SOLID principles have real costs** — Microsoft chose safety over testability
4. **Composition and inheritance are both tools** — Use the right one for the job

The next time Moq fails on a non-virtual member, don't just be frustrated. Ask *why* it's not virtual, and choose your workaround accordingly.

## Quick Reference

```csharp
// Won't work - Uri is not virtual
var mock = new Mock<NavigationManager>();
mock.Setup(x => x.Uri).Returns("test");  // THROWS!

// Works - inherit and use protected methods
class TestNav : NavigationManager
{
    public TestNav(string uri) => Initialize(uri, uri);
    protected override void NavigateToCore(string uri, bool forceLoad) { }
}

// Works - wrap in your own interface
interface INavService { string? CurrentUrl { get; } }
var mock = new Mock<INavService>();
mock.Setup(x => x.CurrentUrl).Returns("test");  // SUCCESS!
```

---

*This pattern came up while testing error handling in a .NET MAUI app. The NavigationManager wasn't mockable, but test implementations let us verify behavior without changing production code.*

## Related Posts

- [MAUI Blazor's NavigationManager Not Initialized Gotcha](/blog/maui-blazor-navigationmanager-not-initialized) — The timing bug that made this testing pattern essential
- [Error Boundaries That Don't Trap Users](/blog/error-boundaries-that-dont-trap-users) — The error boundary code we needed to test
