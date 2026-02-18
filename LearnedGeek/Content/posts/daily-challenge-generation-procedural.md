# Daily Challenge Generation: Procedural Content from Typed Generators

Every day at midnight, every active player gets 3 new challenges. No designer hand-placed them — they're generated from typed C# classes.

This is one of those systems that sounds like it should be complicated. Rule engines, DSLs, configuration databases, expression parsers. But [API Combat](https://apicombat.com) generates all of its daily challenges with a handful of small generator classes, a JSON column, and an integer. Here's how.

## The Base Generator Pattern

Every challenge type inherits from a single abstract class:

```csharp
public abstract class BaseChallengeGenerator
{
    public abstract string ChallengeType { get; }
    public abstract string DisplayName { get; }
    public abstract int Weight { get; }

    public abstract DailyChallenge Generate(Player player);
    public abstract void CheckProgress(DailyChallenge challenge, Battle battle);
}
```

Two methods. That's the contract.

`Generate` takes a player and returns a fully constructed `DailyChallenge` — title, description, requirements, rewards, everything. `CheckProgress` takes an existing challenge and a completed battle, then decides whether (and how much) to advance the player's progress.

Why two methods instead of one? Because generation and progress tracking have completely different lifetimes. `Generate` runs once at midnight. `CheckProgress` runs after every single battle, for every active challenge, for the duration of that challenge's life. They need different inputs, different logic, and different performance characteristics.

## The Challenge Model

The `DailyChallenge` entity uses a generic schema — typed logic, generic storage:

```csharp
public class DailyChallenge
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string ChallengeType { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string RequirementsJson { get; set; }
    public int Required { get; set; }
    public int Progress { get; set; }
    public int GoldReward { get; set; }
    public int XpReward { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCompleted => Progress >= Required;
}
```

The key design decision is `RequirementsJson`. It's a JSON string column that stores whatever structured data a particular generator needs. A `TeamCompositionChallenge` might store `{"requiredClass": "Healer", "count": 2}`. An `UnderdogChallenge` might store `{"maxRatingDifference": -200}`. The column is opaque to the database and to the `DailyChallenge` entity — only the generator that created it knows how to read it.

`Progress` and `Required` are plain integers. Every challenge, regardless of type, boils down to "you need X of something, you have Y so far." Win 3 battles? Required = 3, progress starts at 0. Win a flawless victory? Required = 1, progress starts at 0. Use 4 different unit classes? Required = 4, progress tracks how many distinct classes you've used.

This keeps the UI dead simple. The client doesn't need to understand challenge types — it just renders a progress bar from `Progress / Required` and shows the title and description. All the complexity lives server-side in the generators.

## The Generator Roster

Six generators cover the full range of challenge types:

**BattleCountChallengeGenerator** — Win N battles. The bread and butter. Required scales from 2 to 5 based on the player's recent activity level (active players get harder challenges).

```csharp
public class BattleCountChallengeGenerator : BaseChallengeGenerator
{
    public override string ChallengeType => "battle_count";
    public override string DisplayName => "Battle Count";
    public override int Weight => 30;

    public override DailyChallenge Generate(Player player)
    {
        var required = player.BattlesLast7Days switch
        {
            > 50 => 5,
            > 20 => 4,
            > 10 => 3,
            _ => 2
        };

        return new DailyChallenge
        {
            PlayerId = player.Id,
            ChallengeType = ChallengeType,
            Title = $"Win {required} Battles",
            Description = $"Win {required} battles in any mode.",
            RequirementsJson = "{}",
            Required = required,
            GoldReward = 100 + (required * 20),
            XpReward = 50 + (required * 10),
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Date.AddDays(1)
        };
    }

    public override void CheckProgress(DailyChallenge challenge, Battle battle)
    {
        if (battle.WinnerId == challenge.PlayerId)
            challenge.Progress++;
    }
}
```

Notice the `Weight` property — that's 30, the highest weight in the pool. Battle count challenges show up more often because they're universally completable. Every player can win battles. Not every player has a diverse enough roster for a variety challenge.

**FlawlessVictoryChallengeGenerator** — Win a battle without losing any units. Required is always 1. This one's binary: you either pulled it off or you didn't.

```csharp
public override void CheckProgress(DailyChallenge challenge, Battle battle)
{
    if (battle.WinnerId != challenge.PlayerId) return;

    var allUnitsAlive = battle.WinnerUnits.All(u => u.FinalHp > 0);
    if (allUnitsAlive)
        challenge.Progress++;
}
```

**TeamCompositionChallengeGenerator** — Win battles using a specific team composition. "Win 2 battles with at least 2 Healers on your team." The required class and count are stored in `RequirementsJson`:

```csharp
public override DailyChallenge Generate(Player player)
{
    var targetClass = _unitClasses[_random.Next(_unitClasses.Length)];
    var count = _random.Next(2, 4);
    var required = 2;

    return new DailyChallenge
    {
        // ...
        RequirementsJson = JsonSerializer.Serialize(new
        {
            requiredClass = targetClass.ToString(),
            count
        }),
        Required = required,
        Title = $"Class Specialist: {targetClass}",
        Description = $"Win {required} battles with at least {count} {targetClass}s on your team."
    };
}

public override void CheckProgress(DailyChallenge challenge, Battle battle)
{
    if (battle.WinnerId != challenge.PlayerId) return;

    var requirements = JsonSerializer.Deserialize<TeamCompositionRequirements>(
        challenge.RequirementsJson);

    var classCount = battle.WinnerUnits
        .Count(u => u.Class.ToString() == requirements.RequiredClass);

    if (classCount >= requirements.Count)
        challenge.Progress++;
}
```

This is where `RequirementsJson` earns its keep. The database schema doesn't change when we add a new challenge type with different parameters. The `DailyChallenge` table stays generic. The specifics live in the JSON.

**UnderdogChallengeGenerator** — Win a battle against a higher-rated opponent. Stored requirement is the minimum rating gap. Progress checks compare the player's rating against their opponent's at the time of the battle.

**VarietySquadChallengeGenerator** — Win battles using N different unit classes. This one tracks distinct classes across multiple battles, so `CheckProgress` deserializes a set from `RequirementsJson` and adds new classes as the player uses them:

```csharp
public override void CheckProgress(DailyChallenge challenge, Battle battle)
{
    if (battle.WinnerId != challenge.PlayerId) return;

    var state = JsonSerializer.Deserialize<VarietyState>(
        challenge.RequirementsJson);

    var classesUsed = battle.WinnerUnits
        .Select(u => u.Class.ToString())
        .Distinct();

    foreach (var cls in classesUsed)
        state.SeenClasses.Add(cls);

    challenge.RequirementsJson = JsonSerializer.Serialize(state);
    challenge.Progress = state.SeenClasses.Count;
}
```

Notice that `RequirementsJson` serves double duty here — it stores both the original requirements *and* the accumulated state. The variety generator writes back to it after every battle. This avoids adding a separate `StateJson` column that only one generator type would use.

**WinStreakChallengeGenerator** — Win N battles in a row. The hardest challenge type. If you lose, your progress resets to zero:

```csharp
public override void CheckProgress(DailyChallenge challenge, Battle battle)
{
    if (battle.WinnerId == challenge.PlayerId)
        challenge.Progress++;
    else
        challenge.Progress = 0;
}
```

Brutal. But the reward formula compensates — streak challenges pay significantly more because the `Required` value is higher relative to difficulty.

## The Reward Formula

Every generator uses the same reward calculation:

```csharp
GoldReward = 100 + (required * 20),
XpReward = 50 + (required * 10)
```

Base of 100 gold and 50 XP, then scales linearly with the `Required` count. A "Win 2 Battles" challenge pays 140 gold and 70 XP. A "Win 5 Battles" challenge pays 200 gold and 100 XP. A "Win 3 in a Row" streak challenge pays 160 gold and 80 XP — but it's meaningfully harder because one loss resets you.

I considered per-generator reward multipliers (flawless victory paying 2x, for example), but linear scaling turned out to be fair enough. The difficulty is already encoded in the `Required` value — harder challenges have higher required counts. Keeping the formula uniform also means players can intuitively compare challenge value at a glance.

## The Generation Job

Challenge generation runs as a [BackgroundService](/Blog/Post/background-services-shared-hosting) — same pattern as every other scheduled job in the game:

```csharp
public class DailyChallengeGenerationJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyChallengeGenerationJob> _logger;
    private readonly List<BaseChallengeGenerator> _generators;

    public DailyChallengeGenerationJob(
        IServiceProvider serviceProvider,
        ILogger<DailyChallengeGenerationJob> logger,
        IEnumerable<BaseChallengeGenerator> generators)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _generators = generators.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var tomorrow = now.Date.AddDays(1);
            var delay = tomorrow - now;

            await Task.Delay(delay, stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await GenerateChallenges(db, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate daily challenges");
            }
        }
    }

    private async Task GenerateChallenges(AppDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var activePlayers = await db.Players
            .Where(p => p.LastBattleAt >= cutoff)
            .ToListAsync(ct);

        _logger.LogInformation(
            "Generating challenges for {Count} active players", activePlayers.Count);

        foreach (var player in activePlayers)
        {
            var yesterdayTypes = await db.DailyChallenges
                .Where(c => c.PlayerId == player.Id
                    && c.GeneratedAt >= DateTime.UtcNow.Date.AddDays(-1))
                .Select(c => c.ChallengeType)
                .ToListAsync(ct);

            var challenges = SelectGenerators(3, yesterdayTypes)
                .Select(g => g.Generate(player))
                .ToList();

            db.DailyChallenges.AddRange(challenges);
        }

        await db.SaveChangesAsync(ct);
    }

    private List<BaseChallengeGenerator> SelectGenerators(
        int count, List<string> excludeTypes)
    {
        var eligible = _generators
            .Where(g => !excludeTypes.Contains(g.ChallengeType))
            .ToList();

        var selected = new List<BaseChallengeGenerator>();
        var totalWeight = eligible.Sum(g => g.Weight);

        for (var i = 0; i < count && eligible.Count > 0; i++)
        {
            var roll = Random.Shared.Next(totalWeight);
            var cumulative = 0;

            foreach (var generator in eligible)
            {
                cumulative += generator.Weight;
                if (roll < cumulative)
                {
                    selected.Add(generator);
                    totalWeight -= generator.Weight;
                    eligible.Remove(generator);
                    break;
                }
            }
        }

        return selected;
    }
}
```

A few things worth noting.

**The active-player filter.** `LastBattleAt >= cutoff` where cutoff is 7 days ago. If a player hasn't battled in a week, they don't get challenges. This saves database writes — at scale, the majority of registered accounts are dormant. Without this filter, you'd be generating (and storing and expiring) challenges for thousands of players who will never see them.

When a dormant player comes back and plays a battle, their `LastBattleAt` updates, and the next midnight run picks them up again. No manual reactivation, no "welcome back" special logic. They just start getting challenges again naturally.

**Weighted random selection.** Each generator has a `Weight` property. `BattleCountChallengeGenerator` has weight 30. `WinStreakChallengeGenerator` has weight 5. This means battle count challenges appear roughly 6x more often than win streak challenges — which makes sense, because win streaks are much harder to complete and would feel punishing if they appeared constantly.

**Yesterday's type exclusion.** Before selecting generators, the job queries yesterday's challenge types for that player and excludes them from the pool. This prevents the "Win 3 Battles / Win 4 Battles / Win 5 Battles" problem where a player gets three variants of the same challenge type. You'll always see variety in your daily set.

**No duplicate types in a single day.** The `SelectGenerators` method removes each selected generator from the eligible pool after picking it. You can't get two `FlawlessVictory` challenges in the same day.

## Why Typed Generators Instead of a Rule Engine

I briefly considered a data-driven approach — store challenge definitions in a configuration table with columns like `WinCondition`, `ComparisonOperator`, `Threshold`, and evaluate them generically.

That falls apart immediately when you look at what `CheckProgress` actually does across generator types. `BattleCountChallenge` checks `battle.WinnerId`. `FlawlessVictoryChallenge` checks `WinnerUnits.All(u => u.FinalHp > 0)`. `VarietySquadChallenge` accumulates a set of classes across multiple battles and writes state back to the challenge. `WinStreakChallenge` *resets progress on loss*.

These aren't variations of the same rule. They're fundamentally different evaluation strategies. A rule engine expressive enough to handle all of them would be more complex than six small classes — and harder to debug, harder to test, and harder for a new developer to understand.

Each generator is a single file, under 60 lines, with obvious logic. When something goes wrong with streak challenges, I open `WinStreakChallengeGenerator.cs`. When I want to add a new challenge type, I create a new class, inherit from `BaseChallengeGenerator`, implement two methods, and register it in DI. The [battle engine](/Blog/Post/battle-engine-400-lines-csharp) follows the same philosophy — explicit, typed, readable.

## Adding a New Challenge Type

The process is:

1. Create a class that inherits from `BaseChallengeGenerator`
2. Implement `Generate` and `CheckProgress`
3. Register it in DI: `builder.Services.AddSingleton<BaseChallengeGenerator, NewChallengeGenerator>()`

That's it. The generation job discovers all registered generators via constructor injection. The battle processing pipeline calls `CheckProgress` on all active challenges after every battle, dispatching to the correct generator by `ChallengeType`. No configuration files to update, no database migrations, no deployment-time scripts.

## What I'd Change

**Batch the progress checks.** Right now, after every battle, the system loads all active challenges for both players and calls `CheckProgress` on each one. At 3 challenges per player, that's 6 generator invocations per battle — trivial. But if I added weekly challenges, monthly challenges, and achievement tracking on top of dailies, the per-battle overhead would grow. A smarter approach would be to index challenges by type and only invoke generators relevant to the battle outcome.

**Add difficulty tiers.** Currently, difficulty scales with the `Required` count. But a "Win 5 Battles" challenge and a "Win 1 Flawless Victory" challenge have very different difficulty profiles despite similar reward values. An explicit difficulty tier (easy/medium/hard) with guaranteed distribution — one of each per day — would create a more balanced daily experience.

**Persist generation metadata.** If the server restarts at 11:59 PM and the job misses midnight, challenges don't generate until the next night. A `LastGeneratedDate` column per player would let the system catch up on missed days. Not a real problem yet — the [background service architecture](/Blog/Post/background-services-shared-hosting) handles restarts well — but it's a gap I'm aware of.

## Takeaway

Procedural content generation doesn't require procedural content generation *frameworks*. Six typed classes, a weighted random selector, and a nightly batch job produce varied, fair, difficulty-scaled challenges for every active player — with zero designer intervention and zero configuration tables.

The pattern generalizes well beyond games. Anywhere you have "generate N things of varying types with type-specific logic," the abstract-base-class-with-typed-implementations approach beats a generic rule engine. Keep the schema generic. Keep the logic typed. Let polymorphism do what it was designed for.

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Introducing API Combat](/Blog/Post/introducing-api-combat) for the game overview, [Building the Battle Engine in 400 Lines](/Blog/Post/battle-engine-400-lines-csharp) for the combat system, and [10 Background Services on Shared Hosting](/Blog/Post/background-services-shared-hosting) for the job infrastructure that runs challenge generation.*
