# The Design-Time vs Runtime Mental Model

*Part 5 of the Schema-Aware EF Core Migrations series. This post provides the conceptual foundation that makes Parts 1-4 click.*

After four blog posts and countless hours debugging, I finally understand why EF Core schema handling is so confusing: it's actually *two separate systems* that happen to share some code. Once you see them as distinct, everything else falls into place.

## The Two Worlds of EF Core

EF Core operates in two completely different contexts:

```
┌─────────────────────────────────────────────────────────────────┐
│                      DESIGN-TIME                                │
│  When: dotnet ef migrations add, dotnet ef database update      │
│  Who runs it: Developer CLI, CI pipeline                        │
│  Entry point: IDesignTimeDbContextFactory                       │
│                                                                 │
│  Outputs:                                                       │
│  ├── Migration files (*.cs)                                     │
│  ├── Snapshot file (ApplicationDbContextModelSnapshot.cs)       │
│  └── SQL commands executed against database                     │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                       RUNTIME                                   │
│  When: Application startup, web requests, background jobs       │
│  Who runs it: Kestrel, IIS, container                           │
│  Entry point: Program.cs / Startup.cs                           │
│                                                                 │
│  Operations:                                                    │
│  ├── MigrateAsync() - applies pending migrations                │
│  ├── Queries (SELECT, INSERT, UPDATE, DELETE)                   │
│  └── Model comparison (pending changes detection)               │
└─────────────────────────────────────────────────────────────────┘
```

**The key insight**: These two worlds can have *completely different configurations*. Your `IDesignTimeDbContextFactory` and your `Program.cs` are separate code paths. If they don't agree on schema handling, you get mysterious failures.

## What Gets "Baked In" Where

Here's what confused me for days: some things are determined at design-time and stored in files, while others are determined fresh at runtime.

### Baked into Files at Design-Time

| Artifact | What's Stored | When Created |
|----------|---------------|--------------|
| `ApplicationDbContextModelSnapshot.cs` | Full model structure, including `HasDefaultSchema()` if called | `dotnet ef migrations add` |
| `*_Migration.cs` files | Migration operations (CreateTable, AddColumn, etc.) | `dotnet ef migrations add` |

### Determined Fresh at Runtime

| Operation | When Determined | Configuration Source |
|-----------|-----------------|---------------------|
| Which schema to query | Every database operation | `HasDefaultSchema()` in `OnModelCreating` |
| Which schema to create tables in | `MigrateAsync()` execution | `SchemaAwareMigrationsSqlGenerator` |
| Which history table to check | `MigrateAsync()` execution | `MigrationsHistoryTable()` option |
| Model cache key | First context creation per schema | `IModelCacheKeyFactory` |

## The Schema Flow Diagram

Here's how schema flows through both phases:

```
DESIGN-TIME (dotnet ef migrations add)
══════════════════════════════════════

  DesignTimeDbContextFactory
           │
           ▼
  ┌─────────────────────────────┐
  │ DatabaseSettings.Schema     │◄─── Should be EMPTY for snapshot
  │ (passed to DbContext)       │     to stay schema-less
  └─────────────────────────────┘
           │
           ▼
  ┌─────────────────────────────┐
  │ OnModelCreating()           │
  │ HasDefaultSchema(_schema)   │◄─── Only called if schema non-empty
  └─────────────────────────────┘     (we want to SKIP this at design-time)
           │
           ▼
  ┌─────────────────────────────┐
  │ ApplicationDbContext-       │
  │ ModelSnapshot.cs            │◄─── Saved to disk - schema-less!
  │ ToTable("Users", null)      │     Works for ALL environments
  └─────────────────────────────┘


RUNTIME (Application startup + MigrateAsync)
════════════════════════════════════════════

  Program.cs / appsettings.json
           │
           ▼
  ┌─────────────────────────────┐
  │ DatabaseSettings.Schema     │◄─── REAL schema: "dev", "stg", "prod"
  │ (from configuration)        │
  └─────────────────────────────┘
           │
           ├──────────────────────────────────────┐
           ▼                                      ▼
  ┌─────────────────────────────┐    ┌─────────────────────────────┐
  │ OnModelCreating()           │    │ SchemaAwareMigrations-      │
  │ HasDefaultSchema("dev")     │    │ SqlGenerator                │
  │                             │    │ Rewrites: CREATE TABLE      │
  │ For: SELECT, INSERT, etc.   │    │ [dev].[Users]               │
  └─────────────────────────────┘    └─────────────────────────────┘
           │                                      │
           ▼                                      ▼
  ┌─────────────────────────────┐    ┌─────────────────────────────┐
  │ Runtime Model               │    │ Database Tables             │
  │ (cached per schema)         │    │ Created in [dev] schema     │
  └─────────────────────────────┘    └─────────────────────────────┘
```

## The Six Components Mapped to Phases

Now the six components make sense when you see which phase each one serves:

| # | Component | Phase | Problem It Solves |
|---|-----------|-------|-------------------|
| 1 | `HasDefaultSchema()` | Runtime | Queries target correct schema |
| 2 | Empty schema at design-time | Design-time | Snapshot stays schema-less |
| 3 | `MigrationsHistoryTable()` | Both | Each schema tracks its own migrations |
| 4 | `SchemaAwareMigrationsSqlGenerator` | Runtime | DDL operations target correct schema |
| 5 | `IModelCacheKeyFactory` | Runtime | Multi-schema processes don't collide |
| 6 | `MigrationHelper.Environment` | Design-time | Seed scripts use correct schema |

Notice the split:
- **Design-time only**: #2, #6
- **Runtime only**: #1, #4, #5
- **Both phases**: #3

## The Common Trap: Why Cache Key Factory Seems Complete

When you first hit `PendingModelChangesWarning`, you'll find advice to implement `IModelCacheKeyFactory`. It seems to work at first because:

1. You're running locally with schema "local"
2. The snapshot was generated with schema "local" (because you ran migrations locally)
3. Model comparison passes because they match

Then CI fails because:

1. CI runs with schema "dev"
2. The snapshot still has "local" baked in
3. Model comparison fails: "local" ≠ "dev"

**The cache key factory prevents runtime cache collisions between different schemas in the same process. It does NOT prevent snapshot comparison failures.**

```
┌─────────────────────────────────────────────────────────────────┐
│  What IModelCacheKeyFactory DOES solve:                         │
│                                                                 │
│  Process creates context with "dev" schema                      │
│       │                                                         │
│       ▼                                                         │
│  Cache key: (ApplicationDbContext, "dev", false)                │
│       │                                                         │
│       ▼                                                         │
│  Later, creates context with "stg" schema                       │
│       │                                                         │
│       ▼                                                         │
│  Cache key: (ApplicationDbContext, "stg", false)  ◄── Different!│
│                                                                 │
│  No collision. Each gets its own cached model.                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  What IModelCacheKeyFactory does NOT solve:                     │
│                                                                 │
│  Snapshot file on disk:                                         │
│    HasDefaultSchema("local")                                    │
│    ToTable("Users", "local")                                    │
│       │                                                         │
│       ▼                                                         │
│  Runtime model (in CI):                                         │
│    HasDefaultSchema("dev")                                      │
│    ToTable("Users", "dev")                                      │
│       │                                                         │
│       ▼                                                         │
│  EF Core compares them BEFORE caching                           │
│       │                                                         │
│       ▼                                                         │
│  "PendingModelChangesWarning" - models don't match!             │
└─────────────────────────────────────────────────────────────────┘
```

## The Real Solution: Schema-Less Snapshots

The fix is elegant once you understand the two phases:

**At design-time**: Pass empty schema to `DatabaseSettings`. This prevents `HasDefaultSchema()` from being called in `OnModelCreating`. The snapshot is generated without any schema, using `ToTable("Users", (string)null)`.

**At runtime**: Pass the real schema. `HasDefaultSchema()` runs, queries work correctly. The `SchemaAwareMigrationsSqlGenerator` rewrites DDL to target the correct schema.

```csharp
// DesignTimeDbContextFactory.cs - DESIGN-TIME
public ApplicationDbContext CreateDbContext(string[] args)
{
    var schema = GetSchemaFromEnvironment();  // "dev", "stg", etc.

    optionsBuilder.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable("__EFMigrationsHistory", schema));

    optionsBuilder.UseSchemaAwareMigrations(schema);  // SQL generator uses schema

    // CRITICAL: Empty schema for DatabaseSettings
    // This keeps HasDefaultSchema() from polluting the snapshot
    var databaseSettings = Options.Create(new DatabaseSettings { Schema = string.Empty });

    return new ApplicationDbContext(optionsBuilder.Options, databaseSettings);
}

// Program.cs - RUNTIME
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var schema = configuration["Database:Schema"];  // Real schema from config

    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable("__EFMigrationsHistory", schema));

    options.UseSchemaAwareMigrations(schema);
    options.ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>();
});

// DatabaseSettings gets the REAL schema at runtime
builder.Services.Configure<DatabaseSettings>(config => config.Schema = schema);
```

## Tests: The Exception to the Rule

Tests need the real schema for queries to work, but they compare against the schema-less snapshot. This creates an expected mismatch.

The solution: Log the warning instead of throwing.

```csharp
// In test fixtures
optionsBuilder.ConfigureWarnings(w =>
    w.Log(RelationalEventId.PendingModelChangesWarning));
```

This is safe because:
1. The warning is expected (schema-less snapshot vs schema-aware runtime)
2. Migrations apply correctly via `SchemaAwareMigrationsSqlGenerator`
3. The warning is logged for visibility, not silently suppressed

## The Mental Model Checklist

Before you debug a schema issue, ask:

1. **Which phase is failing?** Design-time (`dotnet ef`) or runtime (application)?
2. **What's in the snapshot?** Check `ApplicationDbContextModelSnapshot.cs` for hardcoded schemas
3. **What's the entry point?** `DesignTimeDbContextFactory` or `Program.cs`?
4. **Are both phases configured identically?** (They shouldn't be for schema handling!)

## Summary: The Two-Phase Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    DESIGN-TIME                                  │
│  Goal: Generate schema-less artifacts                           │
│                                                                 │
│  ✓ UseSchemaAwareMigrations(schema) - for SQL generation        │
│  ✓ MigrationsHistoryTable(schema) - for history isolation       │
│  ✓ Empty DatabaseSettings.Schema - keeps snapshot clean         │
│  ✓ MigrationHelper.Environment - for seed scripts               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      RUNTIME                                    │
│  Goal: Apply schema to all operations                           │
│                                                                 │
│  ✓ HasDefaultSchema(schema) - for queries                       │
│  ✓ UseSchemaAwareMigrations(schema) - for DDL                   │
│  ✓ MigrationsHistoryTable(schema) - for history isolation       │
│  ✓ IModelCacheKeyFactory - for multi-schema processes           │
│  ✓ Real DatabaseSettings.Schema - from configuration            │
└─────────────────────────────────────────────────────────────────┘
```

The two phases have different goals. Design-time generates portable artifacts. Runtime applies environment-specific configuration. Once you internalize this split, EF Core schema handling becomes predictable.

---

*Schema-Aware EF Core Migrations series:*

1. *[Schema-Aware EF Core Migrations](/Blog/Post/schema-aware-ef-core-migrations) - The custom SQL generator approach*
2. *[The MigrationsHistoryTable Bug](/Blog/Post/schema-aware-ef-core-migrations-part-2) - Why history table schema matters*
3. *[Hardening Schema Migrations](/Blog/Post/schema-aware-ef-core-migrations-part-3) - Tests that let you sleep at night*
4. *[The Model Cache Key Factory](/Blog/Post/schema-aware-ef-core-migrations-part-4) - Preventing false PendingModelChangesWarning*
5. ***The Design-Time vs Runtime Mental Model** - Why schema handling is actually two systems (this post)*

*Want the no-jargon version? Read the [ELI5: Why Your Database Needs Colored Folders](/Blog/Post/eli5-schema-aware-migrations).*
