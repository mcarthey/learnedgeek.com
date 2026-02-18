# Environmental Modifiers: Rotating the Meta Weekly Without Patching

Every Monday at midnight UTC, the rules of combat in [API Combat](https://apicombat.com) change. No deploy. No code update. No patch notes to download. The game reads a different modifier from the database, and suddenly Mages hit harder, or healing is doubled, or everyone's armor is thicker.

This is how you keep a strategy game fresh without shipping code.

## The Problem

In any competitive game with customizable builds, the meta converges. Players optimize. They find the dominant strategy. They all use it. The game becomes a mirror match — not because the system lacks variety, but because one configuration is mathematically superior.

You can fix this by patching: nerf the dominant build, buff the weak ones. But patches require deployments, testing, and downtime. For a game running on [shared hosting](/Blog/Post/deploying-aspnet-core-shared-hosting) without a CI/CD pipeline, a patch means manually publishing and hoping the IIS app pool cooperates.

Environmental modifiers are a different approach: the game logic doesn't change, but the *context* around it shifts. The same strategy that dominated last week might be mediocre this week because the rules shifted under it.

## The IModifierEffect Interface

Every modifier implements a simple interface:

```csharp
public interface IModifierEffect
{
    void ModifyUnitStats(Unit unit);
    void ApplyToBattle(BattleContext context);
}
```

Two methods. `ModifyUnitStats` runs once per unit before the battle starts — it adjusts base stats like attack, defense, and speed. `ApplyToBattle` runs once at battle start — it sets context-level properties like healing multipliers and custom flags.

The base class provides no-op defaults so each modifier only implements what it needs:

```csharp
public abstract class BaseModifierEffect : IModifierEffect
{
    public virtual void ModifyUnitStats(Unit unit) { }
    public virtual void ApplyToBattle(BattleContext context) { }
}
```

## Real Modifiers

Here are some of the modifiers in rotation:

### Arcane Disruption

Mages are weakened, Warriors and Rangers are empowered:

```csharp
public class ArcaneDisruption : BaseModifierEffect
{
    public override void ModifyUnitStats(Unit unit)
    {
        if (unit.Class == UnitClass.Mage)
        {
            unit.Attack = (int)(unit.Attack * 0.70); // -30% attack
        }
        else if (unit.Class == UnitClass.Warrior || unit.Class == UnitClass.Ranger)
        {
            unit.Attack = (int)(unit.Attack * 1.20); // +20% attack
        }
    }
}
```

This modifier punishes Mage-heavy compositions and rewards physical damage teams. A player whose strategy relies on Mage AOE suddenly needs to rethink their team composition — or accept a disadvantage for the week.

### Heavy Armor

Everyone is tankier, and healing is doubled:

```csharp
public class HeavyArmor : BaseModifierEffect
{
    public override void ModifyUnitStats(Unit unit)
    {
        unit.Defense = (int)(unit.Defense * 1.50); // +50% defense
    }

    public override void ApplyToBattle(BattleContext context)
    {
        context.HealingMultiplier = 2.0;
    }
}
```

Heavy Armor turns games into wars of attrition. Aggressive formations that rely on burst damage lose their edge. Defensive formations with healers become dominant. The [formation modifier math](/Blog/Post/battle-engine-400-lines-csharp) — aggressive gives +15% attack but -10% defense — shifts from marginal to critical when defense is already boosted by 50%.

### Bloodlust

High risk, high reward — crits are more common but units are fragile:

```csharp
public class Bloodlust : BaseModifierEffect
{
    public override void ModifyUnitStats(Unit unit)
    {
        unit.Defense = (int)(unit.Defense * 0.70); // -30% defense
    }

    public override void ApplyToBattle(BattleContext context)
    {
        context.CustomData["CritChance"] = 0.25; // 25% crit (default 10%)
        context.CustomData["CritMultiplier"] = 2.0; // 2x damage (default 1.5x)
    }
}
```

The `CustomData` dictionary is how modifiers inject values that the [battle engine's damage pipeline](/Blog/Post/battle-engine-400-lines-csharp) reads. The engine checks `context.CustomData["CritChance"]` before rolling crits, falling back to the default 10% if the key doesn't exist.

## The Design Constraint

Every modifier must be expressible as either:
1. **Stat multipliers** on individual units (`ModifyUnitStats`)
2. **Battle context flags or multipliers** (`ApplyToBattle`)

No modifier can introduce new game logic. No modifier adds new abilities, changes the turn order algorithm, or modifies how the action priority chain works. This is intentional — it means adding a new modifier is one class and one dictionary entry. No engine changes, no new tests for core logic, no risk of breaking existing battles.

```csharp
// Registry — adding a modifier is one line
public static readonly Dictionary<string, IModifierEffect> Effects = new()
{
    ["arcane_disruption"] = new ArcaneDisruption(),
    ["heavy_armor"] = new HeavyArmor(),
    ["bloodlust"] = new Bloodlust(),
    ["healing_drought"] = new HealingDrought(),
    ["speed_surge"] = new SpeedSurge(),
    ["glass_cannon"] = new GlassCannon(),
    ["fortification"] = new Fortification(),
    ["wild_magic"] = new WildMagic()
};
```

Eight modifiers rotate on an 8-week cycle. Each week feels different. After 8 weeks, the cycle repeats — but by then, the strategy marketplace has new entries and the meta has evolved enough that the same modifier plays differently.

## The Rotation Job

A [background service](/Blog/Post/background-services-shared-hosting) handles the weekly rotation:

```csharp
public class WeeklyModifierRotationJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0 && now.TimeOfDay > TimeSpan.Zero)
                daysUntilMonday = 7;

            var nextMonday = now.Date.AddDays(daysUntilMonday);
            await Task.Delay(nextMonday - now, stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Deactivate current modifier
            var current = await db.ActiveModifiers
                .FirstOrDefaultAsync(m => m.IsActive, stoppingToken);
            if (current != null)
                current.IsActive = false;

            // Activate next in queue
            var next = await db.ModifierQueue
                .OrderBy(m => m.ScheduledWeek)
                .FirstOrDefaultAsync(m => !m.WasActive, stoppingToken);

            if (next != null)
            {
                db.ActiveModifiers.Add(new ActiveModifier
                {
                    ModifierKey = next.ModifierKey,
                    IsActive = true,
                    ActivatedAt = DateTime.UtcNow
                });
                next.WasActive = true;
            }

            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Rotated to modifier: {Modifier}", next?.ModifierKey);
        }
    }
}
```

The job calculates the exact delay to next Monday midnight UTC — no polling, no wasted cycles. When it fires, it deactivates the current modifier and activates the next one from the queue.

## Why Weekly?

Daily rotation was considered and rejected. Here's why:

**Players need time to adapt.** A modifier that lasts 24 hours doesn't give players enough time to rewrite their strategies, test them, and compete. By the time they've optimized for the current modifier, it's already changing. That's frustrating, not engaging.

**Weekly creates conversation.** "What's this week's modifier?" becomes a social moment. Players discuss strategies for the current week on Discord. Guild leaders plan compositions around the active modifier. The weekly cadence creates a rhythm that players anticipate and plan for.

**Weekly matches the strategy decay cycle.** [Marketplace strategies](/Blog/Post/strategy-marketplace-player-economy) decay at 5% per week. A modifier-optimized strategy is perfectly timed — buy it Monday, get a full week of peak performance, then decay starts as the modifier rotates.

## How the Engine Reads Modifiers

The battle engine doesn't know which modifier is active. It receives a `BattleContext` that may or may not have a modifier applied:

```csharp
public async Task<BattleContext> BuildContext(Battle battle)
{
    var context = new BattleContext
    {
        Team1Units = await LoadTeam(battle.Team1Id),
        Team2Units = await LoadTeam(battle.Team2Id),
        Rng = new Random(battle.Id.GetHashCode())
    };

    // Apply active modifier if exists
    var modifier = await _modifierService.GetActiveModifierAsync();
    if (modifier != null)
    {
        context.ActiveModifier = modifier;

        // Modify each unit's stats
        foreach (var unit in context.Team1Units.Concat(context.Team2Units))
            modifier.ModifyUnitStats(unit);

        // Set battle-level context
        modifier.ApplyToBattle(context);
    }

    return context;
}
```

The engine calls `ModifyUnitStats` on every unit and `ApplyToBattle` on the context. After that, the battle resolves normally. The modifier is invisible to the core resolution loop — it's just different starting stats and context values.

This separation means the modifier system can't break battles. Even if a modifier has a bug (negative defense, NaN healing multiplier), the worst case is a weird battle — not a crash. The engine's damage floor (`Math.Max(1, damage)`) and health bounds prevent impossible states.

## Takeaway

If your game balance lives in the database instead of the code, you can change it without a deploy. Environmental modifiers are a data-driven approach to keeping a competitive game fresh — the rules shift, players adapt, and the meta never stagnates.

The constraint — modifiers can only adjust stats and context values, never game logic — is what makes this maintainable. One class, one dictionary entry, no engine changes. The battle engine doesn't care what the modifier does. It just reads the numbers and resolves the fight.

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Building a Turn-Based Battle Engine](/Blog/Post/battle-engine-400-lines-csharp) for the engine that reads these modifiers, [Strategy Marketplace](/Blog/Post/strategy-marketplace-player-economy) for the decay system that keeps strategies fresh, and [10 Background Services on Shared Hosting](/Blog/Post/background-services-shared-hosting) for how the rotation job runs.*
