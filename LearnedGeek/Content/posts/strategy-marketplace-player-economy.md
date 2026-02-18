# Strategy Marketplace: Building an Economy Around Player-Created JSON

Players in [API Combat](https://apicombat.com) write battle strategies as JSON configurations. Formations, target priorities, conditional ability triggers — all defined in a structured document that the [battle engine](/Blog/Post/battle-engine-400-lines-csharp) consumes and executes.

At some point, a natural question emerged: what if players could sell their strategies to each other?

## The Marketplace Model

The strategy marketplace lets players publish their `StrategyConfig` documents — with a price or for free — and other players can browse, preview, buy, and download them.

```csharp
public class MarketplaceStrategy
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string StrategyJson { get; set; }
    public int Price { get; set; }  // In-game gold, 0 = free
    public int Downloads { get; set; }
    public int WinCount { get; set; }
    public int LossCount { get; set; }
    public double AverageRating { get; set; }
    public double EffectivenessMultiplier { get; set; } = 1.0;
    public DateTime PublishedAt { get; set; }
}
```

The key fields are `WinCount`, `LossCount`, and `EffectivenessMultiplier`. The first two track the strategy's real-world performance — not test battles, not simulations, but actual ranked matches played by buyers. The third one is the decay mechanism that keeps the economy alive.

## Sort Modes

Players browse the marketplace using four sort modes:

```csharp
public IQueryable<MarketplaceStrategy> SortStrategies(
    IQueryable<MarketplaceStrategy> query, string sortBy)
{
    return sortBy switch
    {
        "popular" => query.OrderByDescending(s => s.Downloads),
        "rating" => query.OrderByDescending(s => s.AverageRating)
                        .ThenByDescending(s => s.Downloads),
        "winrate" => query.OrderByDescending(s =>
            s.WinCount + s.LossCount > 10
                ? (double)s.WinCount / (s.WinCount + s.LossCount)
                : 0),
        "recent" => query.OrderByDescending(s => s.PublishedAt),
        _ => query.OrderByDescending(s => s.Downloads)
    };
}
```

The win rate sort has a minimum threshold — strategies with fewer than 10 total battles get a win rate of 0 for sorting purposes. This prevents a strategy with 1 win and 0 losses from sitting at #1 with a "100% win rate" based on a single match.

## The Decay Problem

Without intervention, the marketplace has a predictable lifecycle:

1. Someone publishes a dominant strategy
2. Everyone buys it
3. Everyone runs the same strategy
4. The meta stagnates
5. New strategies can't compete because the dominant one has thousands of wins and sits at the top of every sort
6. Players stop engaging with the marketplace

This is a solved problem in game design. The solution is **decay** — time-based erosion that prevents any single strategy from dominating forever.

## Strategy Decay

A background job runs daily at 2 AM UTC:

```csharp
public class StrategyDecayJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate delay to 2 AM UTC
            var now = DateTime.UtcNow;
            var next2Am = now.Date.AddHours(2);
            if (now >= next2Am) next2Am = next2Am.AddDays(1);
            await Task.Delay(next2Am - now, stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var strategies = await db.MarketplaceStrategies
                .Where(s => s.EffectivenessMultiplier > 0.5)
                .ToListAsync(stoppingToken);

            foreach (var strategy in strategies)
            {
                var ageInWeeks = (DateTime.UtcNow - strategy.PublishedAt).TotalDays / 7.0;
                strategy.EffectivenessMultiplier =
                    Math.Max(0.5, 1.0 - (ageInWeeks * 0.05));
            }

            await db.SaveChangesAsync(stoppingToken);
        }
    }
}
```

The formula: `EffectivenessMultiplier = Max(0.5, 1.0 - (ageInWeeks * 0.05))`

- **Week 0:** 100% effectiveness — the strategy works exactly as designed
- **Week 5:** 75% effectiveness — stat bonuses from the strategy are reduced
- **Week 10:** 50% effectiveness — the floor, the strategy still works but at half power
- **Week 20+:** Still 50% — old strategies are never completely useless

The multiplier applies during battle resolution. A strategy with 0.75 effectiveness gets a 0.75x modifier on formation bonuses and conditional ability thresholds. The core logic still works — units still attack, heal, and use abilities — but the fine-tuned optimizations that made it dominant are dulled.

## Why 5% Per Week?

The rate had to thread a needle:

**Too fast** (10-20% per week): Players feel cheated. "I just bought this yesterday and it's already weaker?" Marketplace trust erodes. Nobody buys strategies because they depreciate too quickly.

**Too slow** (1-2% per week): The decay is invisible. A dominant strategy stays dominant for months. The meta stagnates anyway, just slower.

**5% per week** means a strategy is noticeably weaker after a month (80% effectiveness) but still usable. Buyers get 4-6 weeks of strong performance from their purchase — enough to feel the value, not enough to stop innovating.

The 50% floor is equally important. A strategy from three months ago still works at half effectiveness. If someone downloads a free strategy to learn the game, it's not broken — it's just not competitive at high ranks. New players get a functional starting point. Competitive players need to keep updating.

## Currency Flow

The marketplace creates a closed-loop economy:

```
Player A creates a strategy
Player A publishes it for 500 gold
Player B browses marketplace, buys it for 500 gold
Player A receives 500 gold (minus 10% marketplace fee)
Player B uses the strategy in battles
Player B earns gold from wins → can buy more strategies
```

There's no real money involved. Gold is earned from winning battles, completing daily challenges, and season rewards. The marketplace is a gold sink (buyers spend) and gold source (sellers earn, minus the fee). The 10% marketplace fee is a sink that prevents gold inflation.

## The Social Engineering Angle

The decay mechanism creates a second-order effect that we didn't fully anticipate but turned out to be the best part of the feature.

**Decay incentivizes continuous strategy creation.** The #1 strategy today will be #5 in a month. To stay at the top of the "popular" sort, creators need to publish new strategies. This creates a steady stream of marketplace content.

**New content attracts browsers.** Players check the marketplace because there's always something new. Fresh strategies with high effectiveness are more appealing than old ones at 60%.

**Browsers become buyers.** More browsing means more purchases. More purchases mean more gold flowing to creators. More gold means creators are rewarded for publishing — which creates more content.

It's a flywheel: decay → creation → content → traffic → purchases → rewards → more creation.

## What Buyers See

The marketplace listing shows effectiveness clearly:

```json
{
  "id": "strat-456",
  "name": "Mage Assassin Rush",
  "creator": "WizardSlayer42",
  "price": 350,
  "downloads": 234,
  "winRate": 0.67,
  "averageRating": 4.2,
  "effectiveness": 0.85,
  "publishedAt": "2026-03-01T00:00:00Z",
  "tags": ["aggressive", "mage-heavy", "ranked"],
  "_links": {
    "buy": { "href": "/api/v1/marketplace/strat-456/buy", "method": "POST" },
    "preview": { "href": "/api/v1/marketplace/strat-456/preview", "method": "GET" },
    "creator_profile": { "href": "/api/v1/players/WizardSlayer42", "method": "GET" }
  }
}
```

The `effectiveness` field is front and center. A buyer can see that this strategy is at 85% — published about 3 weeks ago — and make an informed decision. Transparency builds trust.

The `preview` link shows the strategy structure (formation, target priorities) without revealing the full configuration. Enough to understand the approach, not enough to copy it for free.

## Takeaway

Game economies need entropy. Without decay, optimization kills creativity — the best strategy stays the best forever, and the marketplace becomes a one-item store.

The same principle applies outside of games. Any system where user-generated content competes for attention benefits from freshness mechanisms. Stack Overflow deprioritizes old answers. Reddit sorts by "hot" not "all-time top." App stores feature new releases.

If your platform has a "top" list, ask yourself: what happens when position #1 never changes? If the answer is "the platform stagnates," you need decay.

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Building a Turn-Based Battle Engine](/Blog/Post/battle-engine-400-lines-csharp) for the engine that executes these strategies, and [Environmental Modifiers: Rotating the Meta Weekly](/Blog/Post/environmental-modifiers-weekly-rotation) for another mechanism that keeps the game fresh.*
