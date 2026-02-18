# Tier Gating with Custom Action Filters (Not Just [Authorize])

`[Authorize]` checks if you're logged in. `[RequiresTier(Premium)]` checks if you're paying.

In [API Combat](https://apicombat.com), free players get the full game. But Premium features — guild wars, batch operations, the Lua scripting engine — need to be gated behind subscription tiers. Not with feature flags. Not with `if` statements scattered across controllers. With a single attribute that reads the player's current tier and either lets the request through or returns a structured response explaining exactly what to do about it.

## The Marker Attribute

First, a simple attribute that declares intent:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresTierAttribute : Attribute
{
    public SubscriptionTier MinimumTier { get; }

    public RequiresTierAttribute(SubscriptionTier minimumTier)
    {
        MinimumTier = minimumTier;
    }
}
```

That's it. No logic. No authorization checks. Just metadata that says "this endpoint requires at least this tier." The heavy lifting happens elsewhere.

Usage looks like what you'd expect:

```csharp
[Authorize]
[RequiresTier(SubscriptionTier.Premium)]
[HttpPost("guild/create")]
public async Task<IActionResult> CreateGuild([FromBody] CreateGuildRequest request)
{
    // Only Premium+ players reach this code
    var playerId = User.GetPlayerId();
    return Ok(await _guildService.CreateAsync(playerId, request));
}
```

`[Authorize]` and `[RequiresTier]` stack naturally. The authorization filter runs first (is this person authenticated?), and the tier filter runs second (are they paying enough?). If they fail auth, they get a 401 before the tier check ever fires.

## The Action Filter

Here's where the actual decision happens:

```csharp
public class TierGatingActionFilter : IAsyncActionFilter
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TierGatingActionFilter> _logger;

    public TierGatingActionFilter(ApplicationDbContext db,
        ILogger<TierGatingActionFilter> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var tierAttribute = endpoint?.Metadata.GetMetadata<RequiresTierAttribute>();

        // No [RequiresTier] attribute — let it through
        if (tierAttribute is null)
        {
            await next();
            return;
        }

        var playerIdClaim = context.HttpContext.User.FindFirst("PlayerId")?.Value;
        if (!Guid.TryParse(playerIdClaim, out var playerId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var player = await _db.Players.FindAsync(playerId);
        if (player is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (player.CurrentTier < tierAttribute.MinimumTier)
        {
            _logger.LogInformation(
                "Tier gate blocked player {PlayerId} ({CurrentTier}) " +
                "from endpoint requiring {RequiredTier}",
                playerId, player.CurrentTier, tierAttribute.MinimumTier);

            context.Result = new ObjectResult(new
            {
                error = "Insufficient subscription tier",
                requiredTier = tierAttribute.MinimumTier.ToString(),
                currentTier = player.CurrentTier.ToString(),
                upgradeUrl = "https://apicombat.com/Premium"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
```

Let's walk through what happens on every request:

1. **Check for the attribute.** `GetEndpoint()?.Metadata.GetMetadata<RequiresTierAttribute>()` pulls the marker attribute from the endpoint's metadata. If there's no `[RequiresTier]`, the filter calls `next()` and gets out of the way. Zero overhead on ungated endpoints.

2. **Resolve the player.** The `PlayerId` claim comes from the JWT (see [Dual Authentication](/Blog/Post/dual-auth-jwt-cookies-aspnet-core) for how we embed game data in tokens). Parse it, look up the player with `FindAsync()` — a single primary key lookup, not a query.

3. **Compare tiers.** `SubscriptionTier` is an enum: `Free = 0`, `Premium = 1`, `PremiumPlus = 2`. The `<` comparison works because enums are integers underneath. If the player's current tier is less than the required tier, they're blocked.

4. **Return a structured 403.** Not just "forbidden." A JSON response with `requiredTier`, `currentTier`, and `upgradeUrl`. A developer building a client against the API can parse this and show a meaningful message — or even deep-link to the upgrade page.

## Why Not a Policy?

ASP.NET Core has authorization policies. You could write:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequiresPremium", policy =>
        policy.RequireClaim("Tier", "Premium", "PremiumPlus"));
});
```

Then use `[Authorize(Policy = "RequiresPremium")]` on endpoints. This works, technically. But it falls apart for API Combat's needs in three ways.

**Policies are binary.** You pass or you fail. The failure response is a 403 with no body — just a status code. The client has no idea *why* it was rejected. Was the token expired? Wrong role? Wrong tier? The 403 doesn't say.

**No structured error responses.** Policies run in the authorization middleware, which returns challenge/forbid results. You can customize the forbid response globally with `IAuthorizationMiddlewareResultHandler`, but that's a single handler for all policies. Different endpoints might need different error shapes.

**No upgrade path in the response.** This is the big one. When a Free player hits a Premium endpoint, the response shouldn't just say "no." It should say "here's what tier you need and here's where to upgrade." That's a conversion opportunity. A policy gives you a locked door. A filter gives you a locked door with a sign that says "Premium members enter here — upgrade at the front desk."

With an action filter, the 403 response is a first-class object you control completely. Add fields, change the shape, include upgrade URLs, localize the error message — it's just an `ObjectResult`.

## What the 403 Looks Like

When a Free player calls `POST /api/v1/guild/create`:

```json
{
  "error": "Insufficient subscription tier",
  "requiredTier": "Premium",
  "currentTier": "Free",
  "upgradeUrl": "https://apicombat.com/Premium"
}
```

Compare that to a raw policy failure:

```
HTTP/1.1 403 Forbidden
Content-Length: 0
```

The first response is actionable. A developer's client can parse it, display "Guild creation requires Premium — Upgrade here," and link to the upgrade page. The second response is a dead end.

This philosophy runs through all of API Combat's error handling. [Rate limit exceeded?](/Blog/Post/rate-limiting-by-subscription-tier) Here's your current limit, your tier, and the retry time. Tier gated? Here's what you need and where to get it. Every error is a signpost, not a wall.

## Why Not Read the Tier from the JWT Claim?

The JWT already has a `Tier` claim. The [rate limiting middleware](/Blog/Post/rate-limiting-by-subscription-tier) reads it directly from the token — no database call. Why doesn't the tier gating filter do the same?

Because the JWT claim can be stale.

JWTs are issued at login and last 8 hours. If a player upgrades from Free to Premium, their current JWT still says `Free` until they refresh the token. Rate limiting reads from the JWT because it's a performance optimization — microsecond reads, no I/O. If the rate limit is slightly wrong for a few minutes after an upgrade, nobody notices.

Tier gating is different. A player upgrades to Premium, immediately tries to create a guild, and gets "Insufficient subscription tier" because the filter read a stale JWT claim. That's a terrible experience. So the filter hits the database — a single `FindAsync()` on the primary key — and reads the *current* tier. One lightweight query per gated request. Worth it for accuracy.

## Registration

The filter registers as a global filter in `Program.cs`:

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add<TierGatingActionFilter>();
});

builder.Services.AddScoped<TierGatingActionFilter>();
```

Global registration means it runs on every controller action, but the first thing it does is check for the `[RequiresTier]` attribute. No attribute? Straight to `next()`. The overhead on ungated endpoints is a single metadata lookup — effectively free.

`AddScoped` ensures the filter gets a fresh `DbContext` per request. If you register it as a singleton, the `DbContext` will be captured from the first request and reused forever — which is a concurrency disaster. Scoped lifetime, scoped dependencies.

## No Feature Flags Needed

Here's what I like most about this pattern: the tier check *is* the feature flag.

Feature flag systems (LaunchDarkly, Azure App Configuration) are great for rolling out features to percentages of users, A/B testing, or kill-switching broken deployments. But for subscription gating, they're overhead.

You don't need a feature flag that says "is guild creation enabled for this user." You need to check if the user is paying for guilds. That information already lives in the `Players` table. The `[RequiresTier]` attribute declares the gate, and the filter reads the player's current subscription. No external service. No configuration to sync. No flag to forget to update when you change tier boundaries.

If you later move guild access from Premium to Free, you change one attribute: `[RequiresTier(SubscriptionTier.Free)]` — or just remove the attribute entirely. The gate is in the code, right next to the endpoint it protects.

## Composition

The full stack on a gated endpoint:

```csharp
[Authorize]                                    // 1. Are you authenticated?
[RequiresTier(SubscriptionTier.PremiumPlus)]   // 2. Are you paying enough?
[HttpPost("battle/batch")]
public async Task<IActionResult> BatchQueueBattles(
    [FromBody] BatchBattleRequest request)
{
    // 3. Business logic — only PremiumPlus players reach here
    var playerId = User.GetPlayerId();
    return Ok(await _battleService.BatchQueueAsync(playerId, request));
}
```

Three concerns, three layers, each doing one thing:

1. **`[Authorize]`** — Authentication. Are you who you say you are? Handled by the JWT/Cookie policy scheme.
2. **`[RequiresTier]`** — Subscription. Are you paying for this feature? Handled by the action filter.
3. **Controller logic** — Business rules. Everything else.

If auth fails, you get a 401. If the tier check fails, you get a structured 403 with upgrade instructions. If both pass, your controller code runs knowing the player is authenticated *and* authorized for this feature.

No `if (user.Tier < Premium) return Forbid()` scattered through your controllers. No service injection to check tiers in every action method. Just attributes.

## Takeaway

Authorization policies are the right tool when access is binary — admin or not, authenticated or not. But when your access control needs to return structured data — what tier is required, what the player currently has, where to upgrade — an action filter gives you full control over the response shape.

The pattern is three pieces: a marker attribute that declares intent, an action filter that enforces it, and a structured error response that tells the caller what to do about it. Each piece is simple. Together, they turn subscription gating from scattered `if` checks into declarative metadata that sits right next to the endpoint it protects.

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Dual Authentication: JWT + Cookies](/Blog/Post/dual-auth-jwt-cookies-aspnet-core) for how we embed tier data in JWT claims, [Hand-Rolling Rate Limiting by Subscription Tier](/Blog/Post/rate-limiting-by-subscription-tier) for tier-aware middleware, and [Introducing API Combat](/Blog/Post/introducing-api-combat) for what this game is all about.*
