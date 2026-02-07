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

After four parts, here's the complete picture:

| Component | Purpose | Registration |
|-----------|---------|--------------|
| `HasDefaultSchema()` | Runtime queries use target schema | `OnModelCreating` |
| `MigrationsHistoryTable()` | History table per schema | `UseSqlServer()` options |
| `UseSchemaAwareMigrations()` | Rewrite CREATE/ALTER/INDEX operations | `DbContextOptionsBuilder` |
| `ReplaceService<IModelCacheKeyFactory>()` | Prevent false pending changes warnings | `DbContextOptionsBuilder` |
| `MigrationHelper.Environment` | Environment-specific seed scripts | `DesignTimeDbContextFactory` |

Each solves a different problem. All five are required for robust schema-per-environment isolation.

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

*With all four pieces in place, you have production-grade multi-tenant schema isolation in EF Core.*
