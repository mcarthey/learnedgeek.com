# Building a Turn-Based Battle Engine in 400 Lines of C#

No game engine. No Unity. No third-party combat framework.

The entire battle system behind [API Combat](https://apicombat.com) runs on a single class called `DeclarativeStrategyEngine` — and it's about 400 lines of C#. Here's how it works.

## The Core Idea

Players don't control their units in real time. They write **strategy configurations** in JSON — formation, target priorities, ability triggers, conditional logic — and the engine resolves the entire battle from that input. Think of it like programming a robot army and watching the simulation play out.

This design was intentional. API Combat is [a game played entirely through REST endpoints](/Blog/Post/introducing-api-combat), so there's no client to render animations or accept click input. The battle engine needs to consume a strategy, resolve every turn deterministically, and return a complete result — all server-side.

## Strategy Configuration

A strategy is a JSON document that describes how your team should fight:

```json
{
  "formation": "aggressive",
  "targetPriority": ["healers", "lowest_hp"],
  "abilities": {
    "Fireball": {
      "when": "enemy_count_gte_2",
      "target": "priority"
    },
    "Heal": {
      "when": "ally_hp_below_50",
      "target": "lowest_hp_ally"
    },
    "Shield Wall": {
      "when": "ally_count_lte_2",
      "target": "self"
    }
  }
}
```

The engine deserializes this into a `StrategyConfig` object. No custom scripting language. No expression parser. Just typed JSON with a finite set of supported conditions and targets.

## Turn Resolution

Each battle plays out in turns. Here's the resolution loop, simplified:

```csharp
public BattleResult ResolveBattle(BattleContext context)
{
    var allUnits = context.Team1Units
        .Concat(context.Team2Units)
        .ToList();

    var turnLog = new List<TurnAction>();
    var turnNumber = 0;

    while (BothTeamsAlive(context) && turnNumber < MaxTurns)
    {
        turnNumber++;

        // Initiative order: sort by Speed stat, tiebreak with seeded RNG
        var turnOrder = allUnits
            .Where(u => u.IsAlive)
            .OrderByDescending(u => u.Speed)
            .ThenBy(u => context.Rng.Next())
            .ToList();

        foreach (var unit in turnOrder)
        {
            if (!unit.IsAlive) continue;

            var action = DetermineAction(unit, context);
            ExecuteAction(action, context);
            turnLog.Add(action);

            if (!BothTeamsAlive(context)) break;
        }
    }

    return BuildResult(context, turnLog);
}
```

The seeded RNG is important. Every battle gets a `Random` instance seeded from the battle ID. Same seed, same battle — which means replays are deterministic. You can replay any battle and get the exact same result.

## Action Priority Chain

When a unit's turn comes up, `DetermineAction()` evaluates what it should do. The priority chain is:

1. **Ultimate ability** — if charged and conditions are met
2. **Class ability** — if off cooldown and conditions are met
3. **Basic attack** — always available as fallback

```csharp
private TurnAction DetermineAction(Unit unit, BattleContext context)
{
    var strategy = context.GetStrategy(unit.TeamId);

    // Check ultimate first
    if (unit.UltimateCharge >= 100)
    {
        var ultimateConfig = strategy.GetAbilityConfig(unit.UltimateAbility);
        if (ultimateConfig == null || EvaluateCondition(ultimateConfig.When, unit, context))
        {
            unit.UltimateCharge = 0;
            var target = ResolveTarget(ultimateConfig?.Target ?? "priority", unit, context);
            return new TurnAction(unit, unit.UltimateAbility, target, isUltimate: true);
        }
    }

    // Check class ability
    if (unit.AbilityCooldown <= 0)
    {
        var abilityConfig = strategy.GetAbilityConfig(unit.ClassAbility);
        if (abilityConfig == null || EvaluateCondition(abilityConfig.When, unit, context))
        {
            unit.AbilityCooldown = unit.ClassAbility.CooldownTurns;
            var target = ResolveTarget(abilityConfig?.Target ?? "priority", unit, context);
            return new TurnAction(unit, unit.ClassAbility, target);
        }
    }

    // Fallback: basic attack
    var basicTarget = ResolveTarget("priority", unit, context);
    return new TurnAction(unit, BasicAttack.Instance, basicTarget);
}
```

Notice the `null` check on ability configs. If a player's strategy doesn't mention an ability, the engine uses it anyway with default targeting. This means brand new players who submit minimal strategies still get reasonable behavior — their healer will still heal, their tank will still tank. You don't need a perfect strategy JSON to play.

## Condition Evaluation

Conditions are string-based identifiers that map to evaluation functions:

```csharp
private bool EvaluateCondition(string? condition, Unit unit, BattleContext context)
{
    if (string.IsNullOrEmpty(condition)) return true;

    var enemies = context.GetEnemies(unit.TeamId).Where(u => u.IsAlive).ToList();
    var allies = context.GetAllies(unit.TeamId).Where(u => u.IsAlive).ToList();

    return condition switch
    {
        "ally_hp_below_50" => allies.Any(a => a.HpPercent < 50),
        "ally_hp_below_30" => allies.Any(a => a.HpPercent < 30),
        "enemy_count_gte_2" => enemies.Count >= 2,
        "enemy_count_gte_3" => enemies.Count >= 3,
        "ally_count_lte_2" => allies.Count <= 2,
        "self_hp_below_50" => unit.HpPercent < 50,
        "self_hp_above_80" => unit.HpPercent > 80,
        "no_allies_have_buff" => !allies.Any(a => a.HasBuff),
        _ => true // Unknown conditions default to true
    };
}
```

This is intentionally a closed set. Players can't write arbitrary expressions — they pick from a menu of supported conditions. This keeps the engine simple, prevents exploits, and makes the strategy JSON self-documenting. If you see `ally_hp_below_50`, you know exactly what it does.

The `_ => true` default is also a design choice. If a player uses a condition we don't recognize (maybe a typo or a future feature), the ability still fires. Fail open, not closed — a misspelled condition shouldn't brick your entire strategy.

## The Damage Pipeline

When an attack connects, damage flows through a multi-stage pipeline:

```csharp
private int CalculateDamage(Unit attacker, Unit defender, Ability ability,
    BattleContext context)
{
    var baseDamage = (int)(attacker.Attack * ability.DamageMultiplier);

    // Formation bonus: aggressive = +15% outgoing, defensive = -15% incoming
    var formationMod = context.GetFormationModifier(attacker.TeamId);
    baseDamage = (int)(baseDamage * formationMod.AttackMultiplier);

    // Class advantage triangle: Warrior > Ranger > Mage > Warrior (±20%)
    var classMod = GetClassAdvantage(attacker.Class, defender.Class);
    baseDamage = (int)(baseDamage * classMod);

    // Critical hit: 10% chance, 1.5x damage
    if (context.Rng.NextDouble() < 0.10)
    {
        baseDamage = (int)(baseDamage * 1.5);
    }

    // Environmental modifier (weekly rotation changes the rules)
    var envMod = context.ActiveModifier?.ModifyDamage(attacker, defender) ?? 1.0;
    baseDamage = (int)(baseDamage * envMod);

    // Defense reduction
    var defenseReduction = defender.Defense * 0.5;
    var finalDamage = Math.Max(1, baseDamage - (int)defenseReduction);

    return finalDamage;
}
```

The pipeline is explicit and ordered. Each modifier applies in sequence — formation, class advantage, crit, environment, defense — so players can reason about the math. No hidden multipliers, no stacking ambiguity.

The class advantage triangle (Warrior beats Ranger, Ranger beats Mage, Mage beats Warrior) adds a rock-paper-scissors layer that rewards team composition diversity. You can't just stack five damage dealers and brute force your way to the top.

The environmental modifier hook is interesting — it connects to the [weekly modifier rotation](/Blog/Post/introducing-api-combat) system where game rules change every Monday at midnight UTC. The engine doesn't need to know *what* the modifier does, just that it exists and returns a damage multiplier. Clean separation.

## Formation Modifiers

Formations are simple but meaningful:

```csharp
private FormationModifier GetFormationModifier(string formation) => formation switch
{
    "aggressive" => new FormationModifier(AttackMultiplier: 1.15, DefenseMultiplier: 0.90),
    "defensive" => new FormationModifier(AttackMultiplier: 0.90, DefenseMultiplier: 1.15),
    "balanced" => new FormationModifier(AttackMultiplier: 1.00, DefenseMultiplier: 1.00),
    "flanking" => new FormationModifier(AttackMultiplier: 1.10, DefenseMultiplier: 0.95),
    _ => new FormationModifier(AttackMultiplier: 1.00, DefenseMultiplier: 1.00)
};
```

Aggressive formation gives +15% attack but -10% defense. Defensive does the opposite. These are small enough to matter over 20+ turns but not so large that one formation dominates. The meta shifts weekly because environmental modifiers change which formation is optimal — `HeavyArmor` modifier making defense twice as effective suddenly makes defensive formation the clear winner.

## Healer Auto-Heal Fallback

One pattern I'm particularly proud of: healers have built-in fallback logic. If a healer's strategy doesn't specify healing conditions (or no strategy was submitted at all), the engine detects the unit's class and adds automatic heal-when-ally-is-hurt behavior:

```csharp
// In DetermineAction, after checking configured abilities:
if (unit.Class == UnitClass.Healer && unit.AbilityCooldown <= 0)
{
    var woundedAlly = context.GetAllies(unit.TeamId)
        .Where(a => a.IsAlive && a.HpPercent < 70)
        .OrderBy(a => a.HpPercent)
        .FirstOrDefault();

    if (woundedAlly != null)
    {
        unit.AbilityCooldown = unit.ClassAbility.CooldownTurns;
        return new TurnAction(unit, unit.ClassAbility, woundedAlly);
    }
}
```

This makes the game approachable for beginners who might submit a strategy without configuring healer behavior. Their healer still heals the most wounded ally. But advanced players can override this with explicit conditions — "only heal if ally below 30%" or "prioritize the tank."

## Draw Resolution

Most battles end when one team is eliminated. But the engine has a turn limit (prevents infinite loops from two defensive healbot teams staring at each other). When the limit is hit:

```csharp
private BattleResult BuildResult(BattleContext context, List<TurnAction> log)
{
    var team1Alive = context.Team1Units.Where(u => u.IsAlive).ToList();
    var team2Alive = context.Team2Units.Where(u => u.IsAlive).ToList();

    if (team1Alive.Count == 0 && team2Alive.Count == 0)
        return BattleResult.Draw(context, log);

    if (team1Alive.Count == 0)
        return BattleResult.Winner(context.Team2Id, context, log);

    if (team2Alive.Count == 0)
        return BattleResult.Winner(context.Team1Id, context, log);

    // Turn limit reached — compare surviving HP totals
    var team1Hp = team1Alive.Sum(u => u.CurrentHp);
    var team2Hp = team2Alive.Sum(u => u.CurrentHp);

    if (team1Hp == team2Hp)
        return BattleResult.Draw(context, log);

    return BattleResult.Winner(
        team1Hp > team2Hp ? context.Team1Id : context.Team2Id,
        context, log);
}
```

Surviving HP sum as tiebreaker means defensive strategies have an edge in drawn-out fights — which is intentional. If you can't kill your opponent, at least keep your team alive.

## Why Not Use a Game Engine?

I get asked this a lot. Unity, Godot, even a lightweight ECS framework — any of these could model turn-based combat.

But API Combat isn't a game that happens to have an API. [The API is the game](/Blog/Post/why-i-built-a-game-with-no-gui). There's no renderer. No physics. No sprite system. The entire "game" is: accept JSON, run math, return JSON. Using a game engine for that would be like bringing a semi-truck to deliver a letter.

C# gives us everything we need: strong typing for the domain model, LINQ for querying unit collections, `System.Text.Json` for serializing strategies and results, and `Random` with seed support for deterministic replays.

400 lines. No dependencies. Fully testable — every battle is a pure function of its inputs.

## What I'd Change

If I were starting over:

**Make conditions composable.** Right now, `ally_hp_below_50` is a single check. I'd love to support `ally_hp_below_50 AND enemy_count_gte_3` — but that means building a mini expression parser, and the complexity isn't justified yet. The current finite set works well enough, and players haven't asked for more.

**Add a simulation mode for condition testing.** Players can already [simulate battles](/Blog/Post/your-first-api-combat-battle) via the API, but there's no way to test a single condition evaluation without running a full battle. A dry-run endpoint would help strategy authors iterate faster.

**Extract the damage pipeline into a configurable chain.** Right now the multiplier order is hardcoded. A `IDamageModifier` pipeline would make it easier to add new modifiers — but again, the current approach is simple, readable, and works. I'll refactor when the complexity demands it.

## Takeaway

You don't need a game engine to build a game. You need a clear domain model and a resolution loop. If your game is turn-based, deterministic, and server-side, a few hundred lines of business logic will take you further than any framework.

The `DeclarativeStrategyEngine` processes every battle on [apicombat.com](https://apicombat.com) — ranked matches, casual games, guild wars, tournaments, AI bot fights — all through the same 400-line class. Sometimes the simplest architecture is the right one.

---

*This post is part of a series about building API Combat. See also: [Introducing API Combat](/Blog/Post/introducing-api-combat) for the game overview, [Your First Battle](/Blog/Post/your-first-api-combat-battle) for a hands-on walkthrough, and [Why I Built a Game With No GUI](/Blog/Post/why-i-built-a-game-with-no-gui) for the philosophy behind the design.*
