Your app logs an error. You show the user a reference ID so they can call support:

```
Error Reference: 7f3a91b0-4c2e-4d8f-a1b3-9c0d1e2f3a4b
```

The user calls and tries to read it over the phone:

> "Seven... eff... three... ay... nine... one... bee... zero... wait, was that a zero or the letter O? And is that a one or a lowercase L?"

This is a disaster. GUIDs, hex strings, and base64 all have characters that sound alike or look alike. When humans communicate these IDs verbally or type them on a phone keyboard, errors multiply.

## Douglas Crockford's Solution

Douglas Crockford (of JSON fame) designed a Base32 encoding specifically for human readability. His insight: **exclude characters that humans confuse**.

### The Crockford Base32 Alphabet

```
0 1 2 3 4 5 6 7 8 9 A B C D E F G H J K M N P Q R S T V W X Y Z
```

Notice what's **missing**:
- **I** — Looks like `1` (one) and `l` (lowercase L)
- **L** — Looks like `1` (one) and `I` (uppercase i)
- **O** — Looks like `0` (zero)
- **U** — Sounds like "you" and can be confused with `V`

### The Difference Over the Phone

| Encoding | ID | Phone Confusion |
|----------|-----|-----------------|
| Hex | `A1B0C10D` | "Is that one-zero or I-O? And a one or an L?" |
| Base64 | `QWxhZGRpbg==` | "Uppercase or lowercase? Plus or equals?" |
| **Crockford** | `REF-ABCD5678` | "Romeo-Echo-Foxtrot-dash-Alfa-Bravo-Charlie-Delta-five-six-seven-eight" |

The Crockford ID is unambiguous. No zero that looks like O, no one that looks like L.

## Implementation

Here's a C# implementation for generating support-friendly reference IDs:

```csharp
public static class ReferenceIdGenerator
{
    // Crockford's Base32 alphabet - excludes I, L, O, U
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly Random _random = new();
    private static readonly object _lock = new();

    public static string Generate(string prefix = "REF")
    {
        var chars = new char[8];

        lock (_lock)
        {
            for (int i = 0; i < 8; i++)
            {
                chars[i] = Alphabet[_random.Next(Alphabet.Length)];
            }
        }

        return $"{prefix}-{new string(chars)}";
    }

    public static bool IsValid(string? referenceId, string prefix = "REF")
    {
        if (string.IsNullOrEmpty(referenceId))
            return false;

        if (!referenceId.StartsWith($"{prefix}-"))
            return false;

        var expectedLength = prefix.Length + 1 + 8; // prefix + dash + 8 chars
        if (referenceId.Length != expectedLength)
            return false;

        var suffix = referenceId.Substring(prefix.Length + 1);
        return suffix.All(c => Alphabet.Contains(c));
    }

    /// <summary>
    /// Normalizes user input - handles common mistypes.
    /// Crockford spec allows treating I/L as 1 and O as 0.
    /// </summary>
    public static string Normalize(string input)
    {
        return input.ToUpperInvariant()
            .Replace('I', '1')
            .Replace('L', '1')
            .Replace('O', '0');
    }
}
```

## The Prefix Matters

Using a prefix like `REF-` or `ERR-` has several benefits:

1. **Identification** — Support knows immediately what system it's from
2. **Searchability** — Easy to grep logs: `grep "ERR-"`
3. **User confidence** — Users know they're reading the right thing
4. **Disambiguation** — Won't be confused with other IDs in the system

Different contexts might use different prefixes:
- `ERR-` for errors
- `TKT-` for support tickets
- `ORD-` for order numbers
- `INV-` for invite codes

## Entropy and Uniqueness

With 8 characters from a 32-character alphabet:

```
32^8 = 1,099,511,627,776 possible combinations (~1.1 trillion)
```

For a logging system generating thousands of IDs per day, this is more than sufficient. You'd need to generate 1 million IDs per second for 12 days to have a 50% chance of collision.

Need more entropy? Extend to 10 characters (32^10 ≈ 1 quadrillion).

## The Phone Support Flow

Here's how this plays out in practice:

**User's screen:**
```
Something went wrong

Reference ID: ERR-QB20S0VJ

Provide this ID when contacting support.
```

**Support call:**

> **User:** "I got an error, the code is Echo-Romeo-Romeo-dash-Quebec-Bravo-two-zero-Sierra-zero-Victor-Juliet"
>
> **Support:** *types ERR-QB20S0VJ* "Got it, let me look that up..."

No ambiguity. The user can even use NATO phonetic alphabet naturally because there's no I, L, O, or U to confuse things.

## Handling User Mistakes

Even with Crockford encoding, users might still type `O` instead of `0`. The spec allows for this—implement a normalizer:

```csharp
// User types: "ERR-QB2OS0VJ" (accidentally typed O instead of 0)
var normalized = ReferenceIdGenerator.Normalize("ERR-QB2OS0VJ");
// Result: "ERR-QB20S0VJ" (O converted to 0)
```

This forgiveness makes support lookups more reliable.

## Testing

```csharp
public class ReferenceIdGeneratorTests
{
    [Fact]
    public void Generate_ReturnsCorrectFormat()
    {
        var id = ReferenceIdGenerator.Generate("ERR");

        Assert.StartsWith("ERR-", id);
        Assert.Equal(12, id.Length); // "ERR-" + 8 chars
    }

    [Fact]
    public void Generate_OnlyUsesCrockfordCharacters()
    {
        var id = ReferenceIdGenerator.Generate();
        var suffix = id.Split('-')[1];

        Assert.DoesNotContain("I", suffix);
        Assert.DoesNotContain("L", suffix);
        Assert.DoesNotContain("O", suffix);
        Assert.DoesNotContain("U", suffix);
    }

    [Theory]
    [InlineData("ct-abcd5678", "CT-ABCD5678")]
    [InlineData("CT-ABCDIO78", "CT-ABCD1078")]  // I->1, O->0
    [InlineData("CT-ABCDLO78", "CT-ABCD1078")]  // L->1, O->0
    public void Normalize_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, ReferenceIdGenerator.Normalize(input));
    }
}
```

## Beyond Error Codes

This pattern works for any human-communicated identifier:

- **Confirmation codes** — "Your confirmation is CONF-HX7P2QRM"
- **Invite codes** — "Join with code TEAM-N4K8VW3D"
- **Short URLs** — `myapp.com/j/QB20S0VJ`
- **Device pairing** — "Enter code: PAIR-5M9THWKQ"

Anywhere humans read, speak, or type IDs, Crockford Base32 reduces errors.

## Key Takeaways

1. **Design for humans** — Encoding isn't just about efficiency; it's about usability
2. **Phone support is real** — Users will read codes over the phone
3. **Forgiveness matters** — Normalize input to handle common mistakes
4. **Prefixes help** — Make IDs self-identifying
5. **Douglas Crockford was right** — Sometimes the best solution is removing the problem characters entirely

---

*Small design choices make big differences in real-world support. When a user's app crashes and they need help, the last thing they should struggle with is reading the error code.*

## Further Reading

- [Douglas Crockford's Base32 Specification](https://www.crockford.com/base32.html)
- [RFC 4648 - Base Encodings](https://tools.ietf.org/html/rfc4648) (standard Base32, not as human-friendly)

## Related Posts

- [Error Boundaries That Don't Trap Users](/blog/error-boundaries-that-dont-trap-users) — Where these reference IDs get displayed to users
- [Tracer Bullet Development: Prove Your Pipeline](/blog/tracer-bullet-development-prove-your-pipeline) — How error logging with reference IDs proved the architecture
