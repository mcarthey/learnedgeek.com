# Elo Rating + Seasonal Tiers: Two Parallel Rating Tracks

Your global rating is forever. Your seasonal rating resets every 8 weeks. Here's why we run both.

[API Combat](https://apicombat.com) has two rating systems running side by side, and every time I explain this to someone, I get the same question: "Why not just use one?" The answer is that they solve different problems. Global Elo is your permanent skill fingerprint. Seasonal tiers are your fresh-start competitive loop. Both are necessary, and the interplay between them drives everything from matchmaking to rewards.

Let me walk through the implementation.

## Standard Elo: The Permanent Track

The Elo rating system was invented for chess, and it works beautifully for 1v1 strategy games. The core idea: every player has a numeric rating. When two players fight, the outcome shifts their ratings based on who was expected to win.

Here's the formula. `ExpectedScore` calculates the probability that Player A beats Player B, given their current ratings:

```csharp
public static double ExpectedScore(double ratingA, double ratingB)
{
    return 1.0 / (1.0 + Math.Pow(10, (ratingB - ratingA) / 400.0));
}
```

That `/400.0` is the standard Elo scale factor. A 400-point rating gap means the higher-rated player is expected to win about 91% of the time. A 200-point gap is roughly 75%. Equal ratings give 50/50.

The actual rating update after a battle:

```csharp
public (int newRatingA, int newRatingB) CalculateNewRatings(
    int ratingA, int ratingB, BattleOutcome outcome)
{
    var expected = ExpectedScore(ratingA, ratingB);

    // Actual score: 1.0 for win, 0.5 for draw, 0.0 for loss
    double actualA = outcome switch
    {
        BattleOutcome.PlayerAWins => 1.0,
        BattleOutcome.Draw => 0.5,
        BattleOutcome.PlayerBWins => 0.0,
        _ => throw new ArgumentException($"Unknown outcome: {outcome}")
    };

    double actualB = 1.0 - actualA;

    int kA = GetKFactor(ratingA, battlesPlayedA);
    int kB = GetKFactor(ratingB, battlesPlayedB);

    int newA = Math.Max(EloFloor, (int)Math.Round(ratingA + kA * (actualA - expected)));
    int newB = Math.Max(EloFloor, (int)Math.Round(ratingB + kB * (actualB - (1.0 - expected))));

    return (newA, newB);
}
```

A few design decisions worth calling out:

**K-factor of 32** is the standard. But we vary it:

```csharp
private const int EloFloor = 100;

private int GetKFactor(int rating, int battlesPlayed)
{
    if (battlesPlayed < 20) return 40;  // New players: bigger swings
    if (_isPremium) return 24;           // Premium: more stable
    return 32;                           // Standard
}
```

New players with fewer than 20 battles get K=40 so they climb (or fall) faster to their true skill level. Nobody wants to grind 50 games at a rating that doesn't reflect their ability. Premium players get K=24 — slightly more stable ratings that don't swing as hard on a single loss. Standard players sit at K=32, the classic value.

**The floor at 100** prevents ratings from going negative. Nobody wants to see `-47` next to their name. Hitting 100 is demoralizing enough — we don't need to make it worse.

## Seasonal Tiers: The Fresh-Start Track

Global Elo is great for long-term skill measurement, but it has a problem: after a few hundred battles, your rating barely moves. You've converged. The excitement of climbing fades.

Enter seasonal tiers. Every 8 weeks, a new season starts. Everyone's seasonal rating resets. Fresh slate. New leaderboard. New goals.

The `PlayerSeasonRank` table tracks everything per season:

```csharp
public class PlayerSeasonRank
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int SeasonId { get; set; }
    public int SeasonRating { get; set; }
    public int PeakRating { get; set; }
    public SeasonTier CurrentTier { get; set; }
    public SeasonTier PeakTier { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public DateTime LastBattleAt { get; set; }
}
```

Notice `PeakRating` and `PeakTier`. These are separate from the current values, and they matter for end-of-season rewards. More on that in a minute.

### Tier Thresholds

Seasonal tiers map rating ranges to named ranks:

```csharp
public enum SeasonTier
{
    Bronze,     // 0 - 399
    Silver,     // 400 - 799
    Gold,       // 800 - 1199
    Platinum,   // 1200 - 1499
    Diamond,    // 1500 - 1799
    Legend      // 1800+
}

public static SeasonTier GetTierForRating(int rating) => rating switch
{
    < 400 => SeasonTier.Bronze,
    < 800 => SeasonTier.Silver,
    < 1200 => SeasonTier.Gold,
    < 1500 => SeasonTier.Platinum,
    < 1800 => SeasonTier.Diamond,
    _ => SeasonTier.Legend
};
```

Everyone starts at 0 in Bronze. Climb to 1800 and you hit Legend. The thresholds are deliberately wider at the bottom (400 points per tier for Bronze through Gold) and narrower at the top (300 points for Platinum and Diamond). This means early progress feels fast — you blow through Bronze in a few wins — but the final push to Legend is a grind. That's intentional. Legend should mean something.

### Season Auto-Creation

Seasons create themselves. No manual intervention, no admin panel, no cron job to forget about:

```csharp
public async Task<Season> GetOrCreateCurrentSeason()
{
    var now = DateTime.UtcNow;
    var current = await _context.Seasons
        .Where(s => s.StartDate <= now && s.EndDate > now)
        .FirstOrDefaultAsync();

    if (current != null) return current;

    var previous = await _context.Seasons
        .OrderByDescending(s => s.EndDate)
        .FirstOrDefaultAsync();

    var seasonNumber = (previous?.SeasonNumber ?? 0) + 1;
    var startDate = previous?.EndDate ?? now;

    var newSeason = new Season
    {
        SeasonNumber = seasonNumber,
        Name = GetSeasonName(seasonNumber),
        StartDate = startDate,
        EndDate = startDate.AddDays(56), // 8 weeks
        IsActive = true
    };

    _context.Seasons.Add(newSeason);
    await _context.SaveChangesAsync();

    return newSeason;
}
```

Each season gets a name from a rotating pool:

```csharp
private static readonly string[] SeasonNames =
{
    "Dawn of Battle",
    "Rising Storm",
    "Iron Conquest",
    "Shadow Offensive",
    "Crimson Siege",
    "Frost Campaign",
    "Thunder Reign",
    "Ember Trials"
};

private static string GetSeasonName(int seasonNumber)
{
    var index = (seasonNumber - 1) % SeasonNames.Length;
    return SeasonNames[index];
}
```

Season 1 is "Dawn of Battle." Season 9 loops back to "Dawn of Battle." The names are thematic enough to feel like events without requiring someone to write copy every 8 weeks.

## Peak Tracking: Why Final Rating Doesn't Matter

Here's a design lesson I learned from watching League of Legends seasons: end-of-season tanking is miserable. Players hit their goal tier, then stop playing ranked — or worse, tilt on the last day and drop a tier right before rewards lock in.

So in API Combat, **rewards are based on your peak, not your final rating**.

```csharp
public void UpdateSeasonRating(PlayerSeasonRank rank, int newRating)
{
    rank.SeasonRating = newRating;
    var newTier = GetTierForRating(newRating);
    rank.CurrentTier = newTier;

    if (newRating > rank.PeakRating)
    {
        rank.PeakRating = newRating;
    }

    if (newTier > rank.PeakTier)
    {
        rank.PeakTier = newTier;
    }
}
```

If you hit Diamond on week 5, experiment with a wild new strategy, and crash to Gold by week 8 — you still get Diamond rewards. This encourages experimentation. You've already locked in your peak. Go try that all-healer team comp. What's the worst that happens?

## Tier Change Notifications

When a player crosses a tier boundary, they get notified through `INotificationService`. Promotions are celebrations. Demotions are gentle:

```csharp
public async Task HandleTierChange(
    Player player, SeasonTier oldTier, SeasonTier newTier)
{
    if (newTier > oldTier)
    {
        await _notificationService.SendAsync(player.Id, new Notification
        {
            Type = NotificationType.TierPromotion,
            Title = $"Promoted to {newTier}!",
            Message = $"You've reached {newTier} tier. Keep climbing — " +
                      $"next stop: {GetNextTierName(newTier)}!",
            Icon = GetTierIcon(newTier)
        });
    }
    else if (newTier < oldTier)
    {
        await _notificationService.SendAsync(player.Id, new Notification
        {
            Type = NotificationType.TierDemotion,
            Title = $"Tier update: {newTier}",
            Message = $"You've dropped to {newTier}, but your peak of " +
                      $"{player.SeasonRank.PeakTier} is locked in. " +
                      $"Your rewards are safe.",
            Icon = GetTierIcon(newTier)
        });
    }
}
```

The demotion message is deliberate. "Your peak is locked in. Your rewards are safe." It's a gentle nudge, not a slap. The player already feels bad about losing — rubbing it in doesn't help retention. Reminding them that their peak rewards are secure encourages them to keep playing instead of rage-quitting.

## End-of-Season Rewards

When a season ends, rewards are distributed based on peak tier:

| Peak Tier | Gold | XP | Exclusive Title |
|-----------|------|----|-----------------|
| Bronze | 100 | 500 | Bronze Brawler |
| Silver | 250 | 1,200 | Silver Strategist |
| Gold | 500 | 2,500 | Gold Gladiator |
| Platinum | 1,000 | 5,000 | Platinum Commander |
| Diamond | 2,000 | 10,000 | Diamond Warlord |
| Legend | 5,000 | 25,000 | Legendary Conqueror |

Titles are permanent. If you hit Legend in Season 1, you carry "Legendary Conqueror" forever. It's the kind of bragging right that makes people come back season after season.

## The API Tier: Your Global Rank

While seasonal tiers handle competitive resets, your global Elo maps to a separate tier system we call the **API** — Arena Power Index. These are permanent and based on your all-time Elo rating:

| Elo Range | API Tier |
|-----------|----------|
| 100 - 399 | Rubber Duck |
| 400 - 699 | Copy Pasta |
| 700 - 999 | Code Monkey |
| 1000 - 1299 | Bug Hunter |
| 1300 - 1599 | 10x Dev |
| 1600 - 1899 | Wizard |
| 1900+ | I Use Arch btw |

Yes, "I Use Arch btw" is the highest rank. If you've spent enough time in developer communities, you understand why.

The API tiers never reset. Your Rubber Duck shame — or your Wizard glory — follows you forever. Global Elo is a slow-moving measurement of lifetime skill. Seasonal tiers are the sprint. API tiers are the marathon.

## Matchmaking: Bringing It Together

Both rating tracks feed the matchmaking system. Matchmaking uses **global Elo** for pairing (not seasonal rating) because it's a more stable skill indicator, especially early in a season when everyone's seasonal rating is clustered near zero.

The Elo range for matching depends on tier and expands over time:

```csharp
public class MatchmakingConfig
{
    public int InitialRange { get; set; }     // ±200 (Premium) / ±300 (Free)
    public int ExpansionStep { get; set; }     // +50
    public int ExpansionInterval { get; set; } // every 5 seconds
    public int ForceMatchAfter { get; set; }   // 20s (Premium) / 30s (Free)
    public int BotFallbackAfter { get; set; }  // 10s (Premium) / 15s (Free)
}

public async Task<MatchResult> FindMatch(Player player, MatchmakingConfig config)
{
    var elapsed = 0;

    while (elapsed < config.ForceMatchAfter)
    {
        var range = config.InitialRange +
            (elapsed / config.ExpansionInterval) * config.ExpansionStep;

        var opponent = await FindPlayerInRange(
            player.EloRating, range);

        if (opponent != null)
            return MatchResult.Matched(player, opponent);

        // Bot fallback check
        if (elapsed >= config.BotFallbackAfter)
        {
            var bot = await CreateBotOpponent(player.EloRating);
            return MatchResult.BotMatch(player, bot);
        }

        await Task.Delay(1000);
        elapsed++;
    }

    // Force match with widest range or bot
    var lastChance = await FindPlayerInRange(
        player.EloRating, int.MaxValue);

    return lastChance != null
        ? MatchResult.Matched(player, lastChance)
        : MatchResult.BotMatch(player, await CreateBotOpponent(player.EloRating));
}
```

Premium players get tighter initial ranges (±200 vs ±300) because they play more frequently — there are more potential opponents at any given time. Free players get wider ranges to compensate for the smaller active pool.

The expansion step of +50 every 5 seconds means after 10 seconds, a Premium player's range has grown from ±200 to ±300. After 20 seconds, it's ±400. At that point we force-match with whoever is closest, regardless of range.

**Bot fallback** kicks in at 10-15 seconds. If no human opponent is found, we spin up a bot at a similar Elo. The player still earns seasonal rating and rewards — bot matches are real matches — but the Elo change is reduced by 50% to discourage gaming the system by playing only off-peak hours when bots are more likely.

## Why Both Tracks?

I could have built just Elo. Or just seasonal tiers. But they serve different player motivations:

**Global Elo (permanent)** answers: "How good am I overall?" It's your long-term identity. It stabilizes over hundreds of battles. It's what matchmaking uses. It's your API tier — your Rubber Duck or your Wizard. This is for players who want a permanent record.

**Seasonal tiers (resetting)** answer: "What can I achieve this month?" They're the fresh-start dopamine hit. New season, clean slate, everyone back to Bronze. The player who was stuck at Gold last season might hit Platinum this time with a new strategy. This is for players who want goals and rewards on a regular cadence.

Without global Elo, matchmaking would be chaos at the start of every season — 1800-rated players stomping 400-rated players because everyone's seasonal rating reset to zero. Without seasonal tiers, long-term players would stagnate — their rating barely moves, there's no reason to keep competing, no seasonal titles to chase.

The two systems complement each other. Elo provides stability. Seasons provide excitement. Together, they keep both the veteran grinding for "I Use Arch btw" and the newcomer chasing their first Gold season engaged.

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Introducing API Combat](/Blog/Post/introducing-api-combat) for the game overview, [Building a Turn-Based Battle Engine in 400 Lines of C#](/Blog/Post/battle-engine-400-lines-csharp) for the combat system deep dive, and [Teaching REST APIs Through Gaming](/Blog/Post/teaching-rest-apis-through-gaming) for the educational angle.*
