# The Model Cache Key Factory: Preventing False PendingModelChangesWarning

*Part 4 of the Schema-Aware EF Core Migrations series. Read [Part 1](/Blog/Post/schema-aware-ef-core-migrations), [Part 2](/Blog/Post/schema-aware-ef-core-migrations-part-2), and [Part 3](/Blog/Post/schema-aware-ef-core-migrations-part-3) first.*

We thought we had schema-aware migrations figured out. The SQL generator was rewriting table operations, the history table was per-schema, tests were passing. Then EF Core started complaining about "pending model changes"—even though there weren't any.

## The Symptom

On application startup:

```
warn: Microsoft.EntityFrameworkCore.Model.Validation[10632]
      The model for context 'ApplicationDbContext' has pending changes.
      Add a new migration before updating the database.
```

The database was correct. Migrations had run. Tables were in the right schema. But EF Core kept insisting something was wrong.

## Understanding the Root Cause

Here's what we missed: EF Core's schema handling happens in two completely different places.

### 1. SQL Generation (What We Had Solved)

Our `SchemaAwareMigrationsSqlGenerator` rewrites migration operations at SQL generation time:

```csharp
// Input: CREATE TABLE [AspNetUsers] ...
// Output: CREATE TABLE [stg].[AspNetUsers] ...
```

This works beautifully. Tables get created in the correct schema.

### 2. Model Comparison (What We Hadn't Solved)

Before SQL generation even happens, EF Core compares the runtime model against the migration snapshot to detect pending changes. The comparison happens like this:

```
Snapshot Model (generated at migration time):
  HasDefaultSchema("local")
  Tables in "local" schema

Runtime Model (at application startup):
  HasDefaultSchema("stg")  <-- Different!
  Tables in "stg" schema

EF Core: "These models are different! You have pending changes!"
```

The schema is baked into the model. Even though our SQL generator would handle the difference at migration time, EF Core's model comparison sees a mismatch.

## Why Suppressing Warnings Is Wrong

Our first instinct was to suppress the warning:

```csharp
optionsBuilder.ConfigureWarnings(w =>
    w.Ignore(RelationalEventId.PendingModelChangesWarning));
```

This is dangerous. It masks legitimate warnings about actual pending changes. You'd never know if you forgot to create a migration for a new entity.

## The Real Solution: IModelCacheKeyFactory

EF Core caches compiled models by context type. The default cache key is just `typeof(ApplicationDbContext)`. That means every instance of your context—regardless of schema—shares the same cached model.

When the first context is created with schema "local", that model gets cached. When a second context is created with schema "stg", EF Core uses the cached "local" model and detects a mismatch.

The fix is to include the schema in the cache key:

```csharp
public class SchemaAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        // Read schema directly from ApplicationDbContext
        var schema = (context as ApplicationDbContext)?.Schema ?? "prod";
        return new SchemaCacheKey(context, schema, designTime);
    }
}

internal sealed class SchemaCacheKey : IEquatable<SchemaCacheKey>
{
    private readonly Type _contextType;
    private readonly string _schema;
    private readonly bool _designTime;

    public SchemaCacheKey(DbContext context, string schema, bool designTime)
    {
        _contextType = context.GetType();
        _schema = schema;
        _designTime = designTime;
    }

    public override bool Equals(object? obj)
    {
        return obj is SchemaCacheKey other && Equals(other);
    }

    public bool Equals(SchemaCacheKey? other)
    {
        return other is not null
            && _contextType == other._contextType
            && _schema == other._schema
            && _designTime == other._designTime;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_contextType, _schema, _designTime);
    }
}
```

Now each schema gets its own cached model:

```
Cache key for "local" context: (ApplicationDbContext, "local", false)
Cache key for "stg" context:   (ApplicationDbContext, "stg", false)
```

No more false mismatch. No more suppressed warnings. The right solution.

## Registration

Register the factory in both design-time and runtime:

```csharp
// In DesignTimeDbContextFactory
optionsBuilder.ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>();

// In Program.cs
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable("__EFMigrationsHistory", dbSchema));

    options.UseSchemaAwareMigrations(dbSchema);
    options.ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>();
});
```

## Reading Schema From DbContext

Notice the factory reads the schema from `context.Schema`:

```csharp
var schema = (context as ApplicationDbContext)?.Schema ?? "prod";
```

This requires your `ApplicationDbContext` to expose the schema:

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly string _schema;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IOptions<DatabaseSettings>? databaseSettings = null)
        : base(options)
    {
        _schema = databaseSettings?.Value?.Schema ?? "prod";
    }

    /// <summary>
    /// The configured database schema (for logging/debugging and cache key factory).
    /// </summary>
    public string Schema => _schema;
}
```

## Why This Matters for Multi-Tenant Apps

If you're using EF Core with schema-per-tenant (or schema-per-environment like we are), the model cache key factory is essential. Without it, you'll either:

1. Get false `PendingModelChangesWarning` on every startup
2. Suppress the warning and miss real pending changes
3. Overwrite cached models as different tenants/environments use the app

The cache key factory gives you proper isolation with no downsides.

## The Complete Schema-Aware Stack

Across this series, here's the complete picture:

| Component | Purpose | Registration |
|-----------|---------|--------------|
| `HasDefaultSchema()` | Runtime queries use target schema | `OnModelCreating` |
| Empty schema at design-time | Schema-less snapshot generation | `DesignTimeDbContextFactory` |
| `MigrationsHistoryTable()` | History table per schema | `UseSqlServer()` options |
| `UseSchemaAwareMigrations()` | Rewrite CREATE/ALTER/INDEX operations | `DbContextOptionsBuilder` |
| `ReplaceService<IModelCacheKeyFactory>()` | Runtime model caching per schema | `DbContextOptionsBuilder` |
| `MigrationHelper.Environment` | Environment-specific seed scripts | `DesignTimeDbContextFactory` |

Each solves a different problem. All six are required for robust schema-per-environment isolation. See the February 2026 update below for details on the schema-less snapshot requirement.

## Testing the Cache Key Factory

Add tests to verify the cache key behavior:

```csharp
[Fact]
public void SchemaAwareModelCacheKeyFactory_ImplementsInterface()
{
    var factory = new SchemaAwareModelCacheKeyFactory();
    Assert.IsAssignableFrom<IModelCacheKeyFactory>(factory);
}

[Fact]
public void SchemaAwareModelCacheKeyFactory_RegisteredViaReplaceService()
{
    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
    optionsBuilder.ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>();
    // If no exception, registration pattern is valid
}
```

## The Moral

EF Core's schema handling isn't a single problem—it's a constellation of related problems:

1. **SQL generation** - Where tables get created
2. **Query generation** - Where SELECT statements look for tables
3. **Model comparison** - Whether EF Core thinks changes are pending
4. **Model caching** - How EF Core stores compiled models

Our `SchemaAwareMigrationsSqlGenerator` solved #1. `HasDefaultSchema()` solved #2. But #3 and #4 required the model cache key factory.

Don't suppress warnings. Understand why they're firing and fix the root cause.

---

*Schema-Aware EF Core Migrations series:*

1. *[Schema-Aware EF Core Migrations](/Blog/Post/schema-aware-ef-core-migrations) - The custom SQL generator approach*
2. *[The MigrationsHistoryTable Bug](/Blog/Post/schema-aware-ef-core-migrations-part-2) - Why history table schema matters*
3. *[Hardening Schema Migrations](/Blog/Post/schema-aware-ef-core-migrations-part-3) - Tests that let you sleep at night*
4. ***The Model Cache Key Factory** - Preventing false PendingModelChangesWarning (this post)*
5. *[The Design-Time vs Runtime Mental Model](/Blog/Post/schema-aware-ef-core-migrations-part-5) - Why schema handling is actually two systems*

*With all six pieces in place (see the February 2026 update below), you have production-grade multi-tenant schema isolation in EF Core.*

---

## Update: The Missing Piece - Schema-Less Snapshots

*February 2026*

After deploying the cache key factory, CI started failing with `PendingModelChangesWarning`. Turns out we were solving the wrong problem.

### What the Cache Key Factory Actually Solves

The cache key factory prevents **runtime model cache collisions**—when the same process creates contexts with different schemas, each gets its own cached model. This is important for:

- Multi-tenant apps serving different schemas simultaneously
- Tests that run with different schema configurations in the same process

### What It Doesn't Solve

The cache key factory does NOT prevent `PendingModelChangesWarning` triggered by **snapshot comparison**. EF Core compares the runtime model against the snapshot file (`ApplicationDbContextModelSnapshot.cs`) *before* any caching happens.

If your snapshot has `HasDefaultSchema("local")` but you run with schema `"dev"`, EF Core sees a mismatch—regardless of caching.

### The Root Cause

Our `DesignTimeDbContextFactory` was passing the actual schema to `DatabaseSettings`:

```csharp
// OLD (problematic)
var databaseSettings = Options.Create(new DatabaseSettings { Schema = schema });
```

This caused `OnModelCreating` to call `HasDefaultSchema("local")`, which got baked into the snapshot. When CI ran with `"dev"` schema, the models didn't match.

### The Real Fix

Keep the snapshot schema-less by passing empty schema at design-time:

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var schema = environment switch { ... };  // Still compute for SQL generator

        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.MigrationsHistoryTable("__EFMigrationsHistory", schema));

        optionsBuilder.UseSchemaAwareMigrations(schema);  // SQL generator uses schema
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>();

        // CRITICAL: Pass empty schema to DatabaseSettings
        // This prevents HasDefaultSchema() from being called in OnModelCreating
        // The snapshot stays schema-less, compatible with all environments
        var databaseSettings = Options.Create(new DatabaseSettings { Schema = string.Empty });

        return new ApplicationDbContext(optionsBuilder.Options, databaseSettings);
    }
}
```

With this fix:
- **Snapshot**: No schema (tables use `ToTable("X", (string)null)`)
- **SQL Generation**: Schema applied via `SchemaAwareMigrationsSqlGenerator`
- **Runtime Queries**: Schema applied via `HasDefaultSchema()` in `Program.cs`
- **Runtime Caching**: Schema-aware via `SchemaAwareModelCacheKeyFactory`

### Handling Tests

Tests need the actual schema for queries to work, so they'll still see a model/snapshot mismatch. But since migrations apply correctly (via the SQL generator), we can safely log the warning instead of throwing:

```csharp
// In test fixtures only
options.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
```

This is different from `Ignore()` which suppresses completely. `Log()` still records the warning for visibility, but doesn't fail the migration.

### The Complete Six-Component Stack

| Component | Purpose | Where |
|-----------|---------|-------|
| `HasDefaultSchema()` | Runtime queries | `OnModelCreating` (runtime only) |
| Empty schema at design-time | Schema-less snapshot | `DesignTimeDbContextFactory` |
| `MigrationsHistoryTable()` | History table per schema | `UseSqlServer()` options |
| `UseSchemaAwareMigrations()` | Rewrite DDL operations | `DbContextOptionsBuilder` |
| `ReplaceService<IModelCacheKeyFactory>()` | Runtime model caching | `DbContextOptionsBuilder` |
| `MigrationHelper.Environment` | Environment-specific seeds | `DesignTimeDbContextFactory` |

The cache key factory is still essential for runtime isolation. But preventing snapshot contamination is the key to avoiding `PendingModelChangesWarning` in CI.
