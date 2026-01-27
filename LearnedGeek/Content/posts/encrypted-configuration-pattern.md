I have a confession: I once committed an API key to a public GitHub repo.

It was 3 AM. The deploy was "almost done." The key was "temporary." You know how this story ends. Three hours later, my inbox was full of AWS alerts about unauthorized usage, and I was rotating credentials while questioning my life choices.

That incident taught me something important: **secrets in source control are a when problem, not an if problem.** It doesn't matter how careful you are. Eventually, someone on your team (possibly future-you at 3 AM) will make a mistake.

So I built a pattern that makes the mistake impossible: database-backed configuration with encryption, where sensitive values are encrypted at rest and the encryption key never touches the repository.

## The Configuration Problem Everyone Has

Every ASP.NET Core application has secrets. JWT signing keys, API tokens, database credentials. The default approaches all have issues:

- **appsettings.json**: Lives in source control. Even if you gitignore it, someone will eventually commit it.
- **Environment variables**: Visible in process listings, a pain to manage across environments
- **Azure Key Vault / AWS Secrets Manager**: Great if you're all-in on cloud, but adds external dependencies
- **User Secrets**: Development-only. Doesn't help when you're deploying to production.

I wanted something different. Configuration that lives in the database, encrypted at rest, with the encryption key living completely outside the repository. Here's what I built.

## The Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Application Startup                       │
├─────────────────────────────────────────────────────────────────┤
│  1. Read connection string from appsettings.json                 │
│  2. Read encryption key from file (config/crewtrack.key)         │
│  3. Query AppSettings table                                      │
│  4. Decrypt any encrypted values                                 │
│  5. Merge into IConfiguration                                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Database: AppSettings                        │
├──────────┬──────────────────────────┬─────────────┬─────────────┤
│ Key      │ Value                    │ IsEncrypted │ Description │
├──────────┼──────────────────────────┼─────────────┼─────────────┤
│ Jwt:Key  │ AES-GCM encrypted blob   │ true        │ JWT signing │
│ Api:Url  │ https://api.example.com  │ false       │ API base    │
└──────────┴──────────────────────────┴─────────────┴─────────────┘
```

The beauty of this design: your connection string is the only secret in appsettings.json. Everything else lives in the database, encrypted. The encryption key lives in a file that's gitignored, or in an environment variable for CI/CD.

## The Building Blocks

### 1. The AppSetting Entity

Nothing fancy here:

```csharp
public class AppSetting
{
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    public bool IsEncrypted { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

Key naming follows the same hierarchical pattern as appsettings.json. `Jwt:Key` becomes `Configuration["Jwt:Key"]`. Your code doesn't know (or care) where the value came from.

### 2. AES-256-GCM Encryption

I chose AES-256-GCM because it provides both confidentiality and authenticity. If someone tampers with the ciphertext, decryption fails immediately rather than returning garbage.

```csharp
public class AesGcmEncryptionService : IEncryptionService, IDisposable
{
    private const int NonceSize = 12;  // 96 bits (GCM standard)
    private const int TagSize = 16;    // 128 bits (maximum security)
    private const int KeySize = 32;    // 256 bits

    private readonly byte[] _key;
    private bool _disposed;

    public AesGcmEncryptionService(string keyFilePath)
    {
        var keyBase64 = File.ReadAllText(keyFilePath).Trim();
        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != KeySize)
            throw new InvalidOperationException($"Key file must contain {KeySize} bytes");
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // Random nonce is critical - never reuse!
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Output format: nonce || ciphertext || tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedValue)
    {
        if (string.IsNullOrEmpty(encryptedValue)) return string.Empty;

        var data = Convert.FromBase64String(encryptedValue);

        var nonce = new byte[NonceSize];
        var ciphertextLength = data.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(data, NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(data, NonceSize + ciphertextLength, tag, 0, TagSize);

        var plaintext = new byte[ciphertextLength];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
        }
    }
}
```

Note the `CryptographicOperations.ZeroMemory` in Dispose. That zeros out the key in memory when we're done. Security-conscious folks will appreciate that detail.

### 3. The Configuration Provider

This is where it gets clever. ASP.NET Core's configuration system is pluggable. We can add our own provider that reads from the database:

```csharp
public class AppSettingsConfigurationProvider : ConfigurationProvider
{
    private readonly string _connectionString;
    private readonly IEncryptionService _encryptionService;

    public AppSettingsConfigurationProvider(string connectionString, IEncryptionService encryptionService)
    {
        _connectionString = connectionString;
        _encryptionService = encryptionService;
    }

    public override void Load()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        try
        {
            var settings = context.AppSettings.AsNoTracking().ToList();

            foreach (var setting in settings)
            {
                var value = setting.IsEncrypted
                    ? _encryptionService.Decrypt(setting.Value)
                    : setting.Value;

                Data[setting.Key] = value;
            }
        }
        catch (SqlException)
        {
            // Table doesn't exist yet - that's OK during initial migrations
        }
    }
}
```

And an extension method to wire it up:

```csharp
public static IConfigurationBuilder AddAppSettingsFromDatabaseAuto(
    this IConfigurationBuilder builder,
    string connectionString,
    string keyFilePath,
    string? keyEnvironmentVariable = null,
    bool optional = false)
{
    // Try environment variable first (CI/production)
    if (!string.IsNullOrEmpty(keyEnvironmentVariable))
    {
        var keyFromEnv = Environment.GetEnvironmentVariable(keyEnvironmentVariable);
        if (!string.IsNullOrEmpty(keyFromEnv))
        {
            var keyBytes = Convert.FromBase64String(keyFromEnv);
            var encryptionService = new AesGcmEncryptionService(keyBytes);
            builder.Add(new AppSettingsConfigurationSource(connectionString, encryptionService));
            return builder;
        }
    }

    // Fall back to key file (development)
    if (File.Exists(keyFilePath))
    {
        var encryptionService = new AesGcmEncryptionService(keyFilePath);
        builder.Add(new AppSettingsConfigurationSource(connectionString, encryptionService));
        return builder;
    }

    if (!optional)
    {
        throw new InvalidOperationException(
            $"Encryption key not found. Set {keyEnvironmentVariable} or create {keyFilePath}");
    }

    return builder;
}
```

### 4. Wiring It Up in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add encrypted configuration - BEFORE services that read configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Configuration.AddAppSettingsFromDatabaseAuto(
        connectionString,
        "config/crewtrack.key",
        keyEnvironmentVariable: "CREWTRACK_ENCRYPTION_KEY",
        optional: true);
}

// Now configuration includes database values
var jwtKey = builder.Configuration["Jwt:Key"];  // Automatically decrypted!
```

Your services don't know the difference. `IConfiguration` just works.

## The Canary Pattern: Trust But Verify

Here's a trick I picked up: seed a known encrypted value on startup and verify it in your health check:

```csharp
// On startup, seed the canary
if (!await appSettingsService.ExistsAsync("System:EncryptionCanary"))
{
    await appSettingsService.SetSettingAsync(
        "System:EncryptionCanary",
        "CrewTrack encryption verified",
        encrypt: true);
}

// In /api/version endpoint
var canaryValue = await _appSettingsService.GetSettingAsync("System:EncryptionCanary");
var encryptionWorking = canaryValue == "CrewTrack encryption verified";
```

Now your version endpoint tells you if encryption is working:

```json
GET /api/version
{
  "version": "0.9.0-alpha",
  "encryption": {
    "enabled": true,
    "status": "OK",
    "canary": "Verified"
  }
}
```

If the key is wrong or missing, the canary fails. Instant visibility.

## CI/CD Integration

For GitHub Actions, the encryption key lives in repository secrets:

```yaml
env:
  CREWTRACK_ENCRYPTION_KEY: ${{ secrets.CREWTRACK_ENCRYPTION_KEY }}

steps:
  - name: Create encryption key file
    run: |
      mkdir -p config
      echo "${{ secrets.CREWTRACK_ENCRYPTION_KEY }}" > config/crewtrack.key

  - name: Deploy
    run: dotnet publish -c Release
```

The key never touches your repo. It flows from GitHub Secrets → environment variable → config file → application.

## What To Encrypt (And What Not To)

**Encrypt:**
- JWT signing keys
- API tokens and secrets
- Password hashes (if stored in config for some reason)
- Third-party service credentials

**Don't encrypt:**
- URLs (no security benefit, just makes debugging harder)
- Feature flags
- Timeouts and non-sensitive configuration

Encryption adds overhead. Use it where it matters.

## Testing The Pattern

```csharp
[Fact]
public async Task SetSettingAsync_WithEncryption_StoresEncryptedValue()
{
    await _service.SetSettingAsync("Secret:Key", "SecretValue", encrypt: true);

    var setting = await _context.AppSettings.FirstAsync(s => s.Key == "Secret:Key");

    Assert.True(setting.IsEncrypted);
    Assert.NotEqual("SecretValue", setting.Value);  // Stored encrypted
}

[Fact]
public async Task GetSettingAsync_EncryptedSetting_ReturnsDecryptedValue()
{
    await _service.SetSettingAsync("Secret:Key", "SecretValue", encrypt: true);

    var value = await _service.GetSettingAsync("Secret:Key");

    Assert.Equal("SecretValue", value);  // Decrypted on read
}
```

The encrypted value in the database is unreadable garbage. The decrypted value is exactly what you stored. That's the whole point.

## The Payoff

This pattern gives you:

- **Secrets out of source control**: The key file is gitignored. Commit all you want, your secrets are safe.
- **Encryption at rest**: If someone gets database access, they still can't read your JWT key.
- **Environment flexibility**: Same code works with file (dev) or environment variable (CI/production).
- **Seamless integration**: Works with standard `IConfiguration` patterns. No code changes needed in services.
- **Verifiable**: The canary pattern proves encryption is working.

The upfront investment is about an afternoon of work. The peace of mind lasts forever.

Well, until the next 3 AM deploy. But at least this time you won't commit any keys.

---

*Previous: [Modern Database Testing with xUnit Fixtures](modern-database-testing-with-xunit-fixtures) - fast, reliable integration tests with real SQL Server.*

*Next up: [Schema-Aware EF Core Migrations](schema-aware-ef-core-migrations) - running the same migrations across dev, staging, and production schemas.*
