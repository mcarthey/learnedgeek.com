"Which environment did that migration just run against?"

I stared at my colleague's Slack message with the sinking feeling you get when you realize you might have just updated production instead of staging. We were sharing a SQL Server, using different schemas to isolate environments. It was elegant in theory. In practice, EF Core didn't get the memo.

Here's the thing about EF Core migrations: they don't natively support runtime schema selection. The schema is baked in when you *generate* the migration, not when you *apply* it. So my beautiful architecture of `[local]`, `[dev]`, `[stg]`, and `[dbo]` for production was fighting against the framework.

After a few too many "which schema did that actually hit?" moments, I built a solution. It's not pretty under the hood, but it works beautifully in practice.

## The Multi-Environment Setup

Enterprise databases often look like this:

```
                    SQL Server Database
┌─────────────┬─────────────┬─────────────┬─────────────┐
│   [local]   │    [dev]    │    [stg]    │    [dbo]    │
│ Developer   │    CI/CD    │   Staging   │ Production  │
│  sandbox    │   testing   │    UAT      │    live     │
└─────────────┴─────────────┴─────────────┴─────────────┘
```

Same tables, different schemas. The appeal: one database server, complete isolation, easy to compare data across environments.

The reality: EF Core really, *really* wants to put everything in `dbo`.

## Why Standard Approaches Fail

I tried the obvious things first.

**Separate migrations per environment?** Maintenance nightmare. Every schema change means updating four sets of migrations.

**Hardcode schema in migration files?** Same problem, plus you have to remember to change it before running.

**Call `HasDefaultSchema()` in OnModelCreating?** That affects queries, not migrations. The migration is already generated with no schema.

```csharp
// This doesn't affect existing migrations
migrationBuilder.CreateTable(
    name: "WorkOrders",
    // No schema specified - defaults to dbo
    columns: table => new { ... });
```

The schema was missing, and EF Core wasn't going to add it for me.

## The Solution: Intercept and Inject

The insight that unlocked everything: EF Core's SQL generation is pluggable. We can intercept migration operations *before* they become SQL and inject the correct schema.

Here's the architecture:

```
┌──────────────────────────────────────────────────────────┐
│                    MigrateAsync()                        │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│              Migration Operations                        │
│  CreateTableOperation { Schema = null }                  │
│  AddForeignKeyOperation { PrincipalSchema = null }       │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│         SchemaAwareMigrationsSqlGenerator                │
│  ┌────────────────────────────────────────────────────┐  │
│  │ if (schema != "dbo")                               │  │
│  │     operation.Schema = "local" | "dev" | "stg"     │  │
│  └────────────────────────────────────────────────────┘  │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│                      Generated SQL                       │
│  CREATE TABLE [local].WorkOrders (...)                   │
│  ALTER TABLE [local].TaskItems ADD FOREIGN KEY ...       │
└──────────────────────────────────────────────────────────┘
```

We catch the operations mid-flight and fix them.

## The Custom SQL Generator

This is the workhorse:

```csharp
public class SchemaAwareMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
{
    private readonly string? _schema;

    public SchemaAwareMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer,
        IOptions<DatabaseSettings> databaseSettings)
        : base(dependencies, commandBatchPreparer)
    {
        _schema = databaseSettings.Value?.Schema;
    }

    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        // Apply schema to operations that need it
        if (!string.IsNullOrEmpty(_schema) && _schema != "dbo")
        {
            ApplySchemaToOperations(operations);
        }

        return base.Generate(operations, model, options);
    }

    private void ApplySchemaToOperations(IReadOnlyList<MigrationOperation> operations)
    {
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case CreateTableOperation createTable:
                    if (string.IsNullOrEmpty(createTable.Schema))
                        createTable.Schema = _schema;

                    // Foreign keys within table creation need schema too
                    foreach (var fk in createTable.ForeignKeys)
                    {
                        if (string.IsNullOrEmpty(fk.Schema))
                            fk.Schema = _schema;
                        if (string.IsNullOrEmpty(fk.PrincipalSchema))
                            fk.PrincipalSchema = _schema;
                    }

                    // Primary keys, unique constraints
                    if (createTable.PrimaryKey != null &&
                        string.IsNullOrEmpty(createTable.PrimaryKey.Schema))
                        createTable.PrimaryKey.Schema = _schema;

                    foreach (var uc in createTable.UniqueConstraints)
                        if (string.IsNullOrEmpty(uc.Schema))
                            uc.Schema = _schema;
                    break;

                case AddColumnOperation addColumn
                    when string.IsNullOrEmpty(addColumn.Schema):
                    addColumn.Schema = _schema;
                    break;

                case CreateIndexOperation createIndex
                    when string.IsNullOrEmpty(createIndex.Schema):
                    createIndex.Schema = _schema;
                    break;

                case AddForeignKeyOperation addFk
                    when string.IsNullOrEmpty(addFk.Schema):
                    addFk.Schema = _schema;
                    if (string.IsNullOrEmpty(addFk.PrincipalSchema))
                        addFk.PrincipalSchema = _schema;
                    break;

                // ... handle all the other operation types
            }
        }
    }
}
```

Notice we handle `PrincipalSchema` on foreign keys. That one bit me hard. Without it, you get:

```
Foreign key 'FK_AspNetRoleClaims_AspNetRoles_RoleId' references invalid table 'AspNetRoles'
```

Because the FK points to `[dbo].AspNetRoles`, which doesn't exist. Ask me how I know.

## Wiring It Into EF Core's DI System

EF Core needs to know about the schema. We use a custom options extension:

```csharp
public static DbContextOptionsBuilder<TContext> UseSchemaAwareMigrations<TContext>(
    this DbContextOptionsBuilder<TContext> optionsBuilder,
    string schema)
    where TContext : DbContext
{
    // Replace the default SQL generator
    optionsBuilder.ReplaceService<IMigrationsSqlGenerator, SchemaAwareMigrationsSqlGenerator>();

    // Pass schema through options extension
    var extension = optionsBuilder.Options.FindExtension<SchemaOptionsExtension>()
        ?? new SchemaOptionsExtension(schema);

    ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
        extension.WithSchema(schema));

    return optionsBuilder;
}
```

And in your fixture or startup:

```csharp
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(_connectionString, sql =>
    {
        // Migration history goes in schema too
        sql.MigrationsHistoryTable("__EFMigrationsHistory", _schema);
    })
    .UseSchemaAwareMigrations(_schema)
    .Options;
```

## Handling SQL Seed Scripts

EF Core migrations often include raw SQL for seed data. Two patterns here:

### Pattern 1: Environment-Specific Scripts

Files named `SeedAppSettings.local.sql`, `SeedAppSettings.dev.sql`, etc. contain different data per environment. The schema is baked into the filename and SQL:

```sql
-- SeedAppSettings.local.sql
INSERT INTO [local].AppSettings ([Key], [Value], IsEncrypted)
VALUES ('Jwt:Key', 'encrypted-local-key', 1);
```

### Pattern 2: Generic Scripts with Placeholders

For data that's identical across environments, use a placeholder:

```sql
-- SeedRoles.sql (generic)
INSERT INTO {{SCHEMA}}AspNetRoles (Id, Name, NormalizedName)
VALUES
    ('11111111-0000-0000-0000-000000000001', 'Admin', 'ADMIN'),
    ('11111111-0000-0000-0000-000000000002', 'User', 'USER');
```

Replace at runtime:

```csharp
private static string ApplySchemaPlaceholder(string scriptContent)
{
    var schema = Environment switch
    {
        "local" => "[local].",
        "dev" => "[dev].",
        "stg" => "[stg].",
        "prod" => "",  // Production uses dbo, no prefix needed
        _ => "[dev]."
    };

    return scriptContent.Replace("{{SCHEMA}}", schema);
}
```

## Running Migrations Per Environment

```powershell
# Local schema
$env:ASPNETCORE_ENVIRONMENT = "Local"
dotnet ef database update --startup-project ../MyApp.Api

# Staging schema
$env:ASPNETCORE_ENVIRONMENT = "Staging"
dotnet ef database update --startup-project ../MyApp.Api

# Production (dbo)
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update --startup-project ../MyApp.Api
```

Same migrations, different schemas. The SQL generator handles the transformation.

## The Gotchas I Hit So You Don't Have To

### 1. Create Schema First

Non-dbo schemas don't exist by default:

```csharp
if (_schema != "dbo")
{
    await using var schemaCmd = connection.CreateCommand();
    schemaCmd.CommandText = $@"
        IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{_schema}')
            EXEC('CREATE SCHEMA [{_schema}]')";
    await schemaCmd.ExecuteNonQueryAsync();
}
```

### 2. Migrations History Table Location

By default, EF Core puts `__EFMigrationsHistory` in `dbo`. For true schema isolation:

```csharp
optionsBuilder.UseSqlServer(connectionString, sql =>
{
    sql.MigrationsHistoryTable("__EFMigrationsHistory", _schema);
});
```

### 3. HasDefaultSchema in OnModelCreating

This affects query generation, not just migrations:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    if (!string.IsNullOrEmpty(_schema) && _schema != "dbo")
    {
        builder.HasDefaultSchema(_schema);
    }
    // ...
}
```

Without this, your queries will look for tables in `dbo` even though they're in `[dev]`.

## The Payoff

| Aspect | Before | After |
|--------|--------|-------|
| Migration files | One per environment | Single set |
| Schema flexibility | Hardcoded | Runtime |
| SQL scripts | Duplicated | Environment-specific or templated |
| Test isolation | Complex | Natural (unique schema per test class) |
| CI/CD | Multiple pipelines | Single pipeline, environment param |

One set of migrations, one set of scripts, deployed everywhere. No more "which schema did that hit?" moments.

---

This pattern completes the trilogy:

1. [Modern Database Testing](modern-database-testing-with-xunit-fixtures) - Real database testing with xUnit and Respawn
2. [Encrypted Configuration](encrypted-configuration-pattern) - Database-backed secrets with AES-256 encryption
3. **Schema-Aware Migrations** - Multi-environment isolation in shared databases

Together, they give you a robust foundation for enterprise database management. The upfront complexity pays dividends every time you add a new environment or onboard a new developer.

And most importantly: you'll always know which schema you just migrated.
