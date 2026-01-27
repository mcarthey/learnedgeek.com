The test passed. Ship it, right?

I was feeling pretty good about my integration test coverage until I deployed to staging and watched everything catch fire. The LINQ query that worked perfectly against `UseInMemoryDatabase()` generated SQL that SQL Server politely refused to execute. Turns out "politely" in SQL Server means a cryptic error message and a stack trace that scrolls for days.

That's when I learned the hard way: **InMemory databases lie to you.** They're fast and convenient, like a friend who tells you that outfit looks great when it definitely does not.

## The Lies InMemory Tells

For years, the .NET community defaulted to `UseInMemoryDatabase()` for integration tests. Quick setup, fast execution, what's not to love?

Plenty, as it turns out:

- **LINQ translates differently**: What works in memory might generate invalid SQL
- **No constraints**: Foreign keys? Unique indexes? InMemory just shrugs
- **No transactions**: Rollback behavior is simulated, not real
- **Null handling**: SQL Server and InMemory disagree about nulls in ways that will ruin your afternoon

The breaking point came when my team spent three hours debugging a "production bug" that was actually just our tests lying to us about how SQL Server handles null comparisons. The fix took five minutes. Finding it took three hours of questioning reality.

## The Solution: Real Database, Fast Reset

The answer is embarrassingly simple: use a real SQL Server.

"But that's slow!" I hear you protest. It was, until I discovered Respawn.

Here's the magic: instead of dropping and recreating your database for each test (slow), or manually deleting rows in the right order (tedious and error-prone), Respawn analyzes your foreign key relationships once, then deletes data in dependency order in about 20 milliseconds.

Twenty. Milliseconds.

Compare that to `EnsureDeleted()` followed by `EnsureCreated()`, which takes 500-2000ms. That's the difference between a test suite that runs while you're still reaching for your coffee and one that makes you go get a refill.

## The Architecture

The setup uses xUnit's `IClassFixture<T>` pattern. Each test class gets its own database (so tests can run in parallel without stepping on each other), and Respawn handles the cleanup between individual tests.

```
Test Class A → Database_abc123 (parallel)
Test Class B → Database_def456 (parallel)
Test Class C → Database_ghi789 (parallel)
```

The fixture handles three things:
1. **Create a unique database** when the test class starts
2. **Provide Respawn** for fast resets between tests
3. **Drop the database** when the test class finishes

## The Key Insight: Fresh Contexts

Here's the gotcha that cost me 45 minutes of confused debugging: when you save an entity and then query for it using the same DbContext, you might get the cached in-memory version back instead of actually hitting the database.

```csharp
// This might lie to you
await _context.SaveChangesAsync();
var saved = await _context.WorkOrders.FindAsync(workOrder.Id);
// 'saved' could be the cached object, not what's in the database

// This tells the truth
await using var freshContext = _fixture.CreateContext();
var saved = await freshContext.WorkOrders.FindAsync(workOrder.Id);
// 'saved' is definitely from the database
```

Always verify writes with a fresh context. The extra line saves hours of "but I just saved that!" confusion.

## CI Integration

Locally, I use SQL Server LocalDB (free, probably already on your machine if you have Visual Studio). In GitHub Actions, I spin up a SQL Server container.

The fixture detects which environment it's in by checking for a `CI_TEST_CONNECTION_STRING` environment variable. If it exists, we're in CI and use the container. If not, LocalDB it is.

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    env:
      ACCEPT_EULA: Y
      SA_PASSWORD: TestPassword123!
```

The same test code runs in both environments. No conditionals, no separate test configurations.

## The Tradeoffs

Let's be honest about what this approach costs:

| Aspect | InMemory | Real Database |
|--------|----------|---------------|
| Setup complexity | None | Container in CI |
| Speed per test | ~5ms | ~20ms |
| Confidence | "It works on my machine" | Actually works |

That extra 15ms per test and the CI container setup are worth it the first time your staging deployment doesn't catch fire because your tests actually tested reality.

## Quick Wins

**Seed reference data once**: If every test needs the same lookup data (roles, statuses, etc.), seed it in `InitializeAsync()` right after the Respawn reset. Don't re-seed in every test method.

**Meaningful database names**: I use `ProjectName_Test_{Guid}` so when something goes wrong, I can find the database and poke around. `TestDb_1` tells you nothing.

**Don't forget the cleanup**: The fixture's `DisposeAsync()` should drop the test database. Otherwise you'll end up with hundreds of orphaned databases and a very confused DBA.

---

Moving from InMemory to real database testing was one of the best decisions I made for this project. The setup took an afternoon. The confidence it provides has saved me countless hours of "works locally, fails in production" debugging.

When tests pass now, I actually believe them.

---

*Next up: [Database-Backed Encrypted Configuration](encrypted-configuration-pattern) - moving secrets out of appsettings.json and into encrypted database storage.*
