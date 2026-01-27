"Which environment did that migration just run against?"

I stared at my colleague's Slack message with the sinking feeling you get when you realize you might have just updated production instead of staging.

We were sharing a SQL Server, using different schemas to isolate environments: `[local]` for developer sandboxes, `[dev]` for CI, `[stg]` for staging, and `[dbo]` for production. It was elegant in theory—one database server, complete isolation, easy to compare data across environments.

In practice, EF Core didn't get the memo.

## The Problem: Schemas Are Baked In

Here's the thing about EF Core migrations that nobody tells you upfront: the schema is determined when you *generate* the migration, not when you *apply* it.

When you run `dotnet ef migrations add CreateWorkOrders`, EF Core creates a migration file with operations like:

```csharp
migrationBuilder.CreateTable(
    name: "WorkOrders",
    // Notice: no schema specified
    columns: table => new { ... });
```

No schema means `dbo`. Always. It doesn't matter what environment you're in when you run the migration—if the migration file says "no schema," you're getting `dbo`.

So my beautiful multi-schema architecture was fighting against the framework from day one.

## Why The Obvious Solutions Don't Work

I tried the obvious fixes first:

**Generate separate migrations per environment?** Maintenance nightmare. Every schema change means updating four migration files. Miss one and your environments drift.

**Hardcode schema in the migration?** Same problem, plus you have to remember to change it before each migration run. Spoiler: you won't remember.

**Use `HasDefaultSchema()` in OnModelCreating?** This affects *queries*, not migrations. Your SELECT statements will target the right schema, but your CREATE TABLE statements already happened in `dbo`.

I spent two days on these dead ends before finding the real solution.

## The Insight: SQL Generation Is Pluggable

EF Core's architecture has a beautiful seam right where I needed it. When you call `MigrateAsync()`, here's what actually happens:

1. EF Core loads the migration classes
2. Each migration's `Up()` method generates `MigrationOperation` objects
3. A **SQL generator** converts those operations into actual SQL
4. The SQL executes against your database

That SQL generator? It's a replaceable service.

I can intercept the operations *after* the migration defines them but *before* they become SQL, and inject the correct schema at runtime.

## How It Works

The custom SQL generator inherits from `SqlServerMigrationsSqlGenerator` and overrides the `Generate` method. Before calling the base implementation, it loops through all the operations and sets the schema on any that don't have one.

The key operations to handle:
- `CreateTableOperation` (including its foreign keys, primary keys, and constraints)
- `AddColumnOperation`
- `CreateIndexOperation`
- `AddForeignKeyOperation` (both the table schema AND the principal table schema)

That last one bit me hard. Foreign keys reference other tables. If you set the schema on the table with the FK but not the referenced table, you get:

```
Foreign key 'FK_AspNetRoleClaims_AspNetRoles_RoleId' references invalid table 'AspNetRoles'
```

Because it's looking for `[dbo].AspNetRoles`, which doesn't exist—your roles table is in `[dev]`. Ask me how long that one took to figure out.

## The Schema Flows Through DI

The tricky part is getting the schema value into the SQL generator. EF Core's service replacement happens at DbContext configuration time, but the schema is a runtime value.

The solution is a custom options extension that carries the schema through EF Core's dependency injection system. When you configure your DbContext, you call:

```csharp
optionsBuilder.UseSchemaAwareMigrations("dev");
```

This replaces the default SQL generator with your custom one AND passes the schema value through so it's available when SQL generation happens.

## Don't Forget The Migration History

Here's a gotcha that'll bite you if you're not careful: EF Core tracks which migrations have run in a table called `__EFMigrationsHistory`. By default, that table goes in `dbo`.

If you want true schema isolation, you need to tell EF Core to put the history table in your schema too:

```csharp
optionsBuilder.UseSqlServer(connectionString, sql =>
{
    sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
});
```

Otherwise you'll have one shared history table tracking migrations across all environments, which defeats the isolation and can cause "migration already applied" conflicts.

## SQL Scripts Need Love Too

Migrations often include raw SQL for seed data. That SQL also has hardcoded schemas (or no schemas, which means `dbo`).

I use two patterns:

**Environment-specific scripts** for data that differs per environment. Files named `SeedData.dev.sql` and `SeedData.prod.sql` with the schema baked in.

**Templated scripts** for data that's identical but needs different schemas. The SQL uses a placeholder like `{{SCHEMA}}`, and a helper method replaces it at runtime based on the environment.

The helper checks for an environment-specific file first, falls back to the generic templated version, and throws a helpful error if neither exists.

## Running Migrations

With all this in place, running migrations for different environments is straightforward:

```powershell
# Local development
$env:ASPNETCORE_ENVIRONMENT = "Local"
dotnet ef database update

# Staging
$env:ASPNETCORE_ENVIRONMENT = "Staging"
dotnet ef database update
```

Same migrations, different schemas. The SQL generator handles the transformation invisibly.

## The Gotchas

**Create schemas first**: Unlike `dbo`, custom schemas don't exist by default. Your fixture or startup needs to create the schema before running migrations.

**HasDefaultSchema still matters**: You need this in `OnModelCreating` for *queries* to target the right schema. The SQL generator handles migrations; `HasDefaultSchema` handles runtime queries.

**Test it thoroughly**: Run migrations against all environments and verify the tables ended up in the right schemas. A simple SQL query checking `INFORMATION_SCHEMA.TABLES` will tell you if something snuck into `dbo`.

## When This Pattern Shines

This approach is great when:
- Multiple environments share a database server
- You want to compare data across environments easily (same server = easy queries)
- You're running integration tests that need isolated schemas
- You don't want to maintain multiple migration sets

It's overkill if you have separate database servers per environment. At that point, just let everything be `dbo`.

## The Payoff

One set of migrations. One set of seed scripts. Deployed everywhere with environment-appropriate schemas.

No more "which schema did that hit?" moments. No more maintaining four copies of every migration. No more late-night Slack messages asking if you just broke production.

---

This pattern completes the trilogy:

1. [Modern Database Testing](modern-database-testing-with-xunit-fixtures) - Real database testing with xUnit and Respawn
2. [Encrypted Configuration](encrypted-configuration-pattern) - Database-backed secrets with AES-256 encryption
3. **Schema-Aware Migrations** - Multi-environment isolation in shared databases

Together, they give you a robust foundation for enterprise database management. The upfront complexity pays dividends every time you onboard a new developer or spin up a new environment.

And most importantly: you'll always know which schema you just migrated.
