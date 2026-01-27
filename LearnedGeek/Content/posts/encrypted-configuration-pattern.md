I have a confession: I once committed an API key to a public GitHub repo.

It was 3 AM. The deploy was "almost done." The key was "temporary." You know how this story ends. Three hours later, my inbox was full of AWS alerts about unauthorized usage, and I was rotating credentials while questioning my life choices.

That incident taught me something important: **secrets in source control are a when problem, not an if problem.** It doesn't matter how careful you are. Eventually, someone on your team (possibly future-you at 3 AM) will make a mistake.

So I built a pattern that makes the mistake impossible.

## The Configuration Problem Everyone Ignores

Every ASP.NET Core application has secrets. JWT signing keys, API tokens, database credentials. The default approaches all have blind spots:

**appsettings.json** lives in source control. Yes, you can gitignore it. No, that won't save you when someone commits it anyway, or when you need to share settings across a team.

**Environment variables** are visible in process listings and a pain to manage across environments. Ever tried to debug "why doesn't staging work" when the answer is a typo in an env var set six months ago?

**Azure Key Vault / AWS Secrets Manager** are great if you're all-in on cloud. But they add external dependencies and require internet connectivity. Sometimes you just want a simpler solution.

**User Secrets** are development-only. They don't help when you're deploying to production.

## The Idea: Database + Encryption

Here's what I built: configuration that lives in the database, encrypted at rest, with the encryption key living completely outside the repository.

The flow looks like this:

1. App starts, reads connection string from appsettings.json (the *only* secret there)
2. App reads encryption key from a file that's gitignored (or an env var in CI)
3. App queries the `AppSettings` table
4. Encrypted values get decrypted on the fly
5. Everything merges into `IConfiguration`

Your services never know the difference. `Configuration["Jwt:Key"]` just works, whether that value came from appsettings.json, an environment variable, or an encrypted database row.

## Why AES-256-GCM?

I chose AES-256-GCM for the encryption because it provides both confidentiality *and* authenticity. That second part matters: if someone tampers with the ciphertext, decryption fails immediately rather than returning corrupted garbage that might slip through validation.

The encrypted value is a blob containing the nonce, ciphertext, and authentication tag all concatenated and base64-encoded. You can store it in a regular `nvarchar` column without any special handling.

## The Canary Pattern

Here's a trick I learned the hard way: how do you know encryption is actually working?

I seed a "canary" value on startup—a known string that gets encrypted and stored. Then my version endpoint checks if it decrypts correctly:

```json
GET /api/version
{
  "version": "0.9.0-alpha",
  "encryption": {
    "status": "OK",
    "canary": "Verified"
  }
}
```

If the encryption key is wrong, missing, or rotated without updating the database, the canary fails. Instant visibility into something that would otherwise silently break.

## What To Encrypt (And What Not To)

Not everything needs encryption. I follow a simple rule: **encrypt things that could be exploited if the database leaks**.

**Encrypt:**
- JWT signing keys (someone could forge tokens)
- API tokens and secrets (someone could impersonate your app)
- Third-party credentials (someone could rack up charges on your accounts)

**Don't encrypt:**
- URLs (no security benefit, just makes debugging harder)
- Feature flags (who cares if an attacker learns you have dark mode)
- Timeouts and non-sensitive config (encryption adds overhead for no benefit)

## The Key Management Problem

The encryption key is now the crown jewel. Lose it and you lose access to all your encrypted settings. Commit it and you've defeated the entire purpose.

I keep it in a file called `config/myapp.key` that's gitignored. In CI/CD, it flows from GitHub Secrets into an environment variable, which the app reads at startup.

```
Developer machine: config/myapp.key (gitignored file)
CI/CD: MYAPP_ENCRYPTION_KEY (GitHub Secret → env var)
Production: Either approach works
```

The key never touches the repo. The encrypted values in the database are useless without it.

## The Integration

ASP.NET Core's configuration system is beautifully pluggable. I wrote a custom `IConfigurationProvider` that queries the AppSettings table and decrypts values on load. It slots in right after the standard providers:

```csharp
builder.Configuration.AddAppSettingsFromDatabase(
    connectionString,
    "config/myapp.key",
    keyEnvironmentVariable: "MYAPP_ENCRYPTION_KEY");
```

From that point on, `IConfiguration` includes the database values. Your services inject `IConfiguration` or `IOptions<T>` as usual. They have no idea some of those values came from an encrypted database row.

That's the elegance of this pattern: it's invisible to the rest of your codebase.

## The Tradeoffs

Let's be honest about the costs:

**Startup time**: The app hits the database during configuration loading. That's an extra query or two at startup. Not noticeable in practice.

**Key rotation**: If you need to rotate the encryption key, you need to re-encrypt all the values. I have a console app for this, but it's a manual process.

**Complexity**: This is more moving parts than just using environment variables. The benefit is protection against the "someone committed secrets" disaster, but you're trading simplicity for safety.

## When This Pattern Shines

This approach works best when:

- You want secrets out of source control *completely*
- You need to share configuration across multiple app instances
- You want audit trails (database = you can track changes)
- You're paranoid about that 3 AM commit (hi, it's me)

If you're a solo developer with a simple app, environment variables are probably fine. If you're on a team with multiple environments and a healthy fear of security incidents, this pattern earns its complexity.

---

The upfront investment is about an afternoon of work. The peace of mind lasts forever.

Well, until the next 3 AM deploy. But at least this time you won't commit any keys.

---

*Previous: [Modern Database Testing with xUnit Fixtures](modern-database-testing-with-xunit-fixtures) - fast, reliable integration tests with real SQL Server.*

*Next up: [Schema-Aware EF Core Migrations](schema-aware-ef-core-migrations) - running the same migrations across dev, staging, and production schemas.*
