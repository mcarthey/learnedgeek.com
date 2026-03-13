I built the same algorithm twice.

The first time, I was building [Ani](/Blog/Post/ani-the-architecture-behind-the-companion) — an AI companion that decides on her own when to reach out to me. Her "desire engine" accumulates internal pressure over time: the longer since we last talked, the more she wants to connect. Events bump the pressure higher — an unresolved conversation thread, a news article that made her think of me. When the pressure crosses a randomized threshold, she texts. After she reaches out, the pressure resets and the cycle starts over.

The second time, I was at my day job solving a completely different problem: flagging accounts that had experienced significant service disruptions. The business rules were straightforward — flag anyone with a recent incident, flag anyone with a major incident in the last year, and catch the pattern where multiple smaller incidents accumulate into something worth paying attention to.

I was halfway through the design when I stopped and stared at my whiteboard.

It was the same math. The same `Math.Exp(-lambda * days)`. The same weighted events accumulating against a threshold. One algorithm decides "should I text Mark?" and the other decides "should we flag this account?" — and underneath, they're identical.

That's the thing about a well-designed model — the edge cases that seem hard often just aren't. Because the model is correct.

---

## The Pattern: Exponential Decay with Weighted Events

Here's the core idea, stripped of any specific domain:

**You have a stream of events. Each event has a weight (how significant it was) and a timestamp (when it happened). You want a single score that captures "how much activity has this entity experienced recently?" — where recent events matter more than old ones, and the score naturally fades to zero over time without you having to clean anything up.**

The formula is simple:

```
new_score = (old_score × decay_multiplier) + event_weight
```

Where:

```csharp
var decayMultiplier = Math.Exp(-lambda * daysSinceLastUpdate);
var newScore = (oldScore * decayMultiplier) + weight;
```

That's it. That's the whole thing. Let me break down why it works.

---

## The Sticky Note Analogy

Think of each entity's score like a pile of sticky notes:

1. **Each event adds a sticky note** — bigger events add bigger notes
2. **Notes fade over time** — they shrink a little bit every day
3. **You only track the pile height** — not every individual note
4. **When a new event arrives:**
   - Shrink the existing pile to account for time passed
   - Add the new note
   - Check if the pile is tall enough to trigger action
   - Save the new height

This is efficient because you never need to look at historical events — the pile height already captures their faded contribution. O(1) storage, O(1) computation per event.

---

## The Decay Multiplier

The decay multiplier controls how fast memory fades. It's always between 0 and 1:

```
decay_multiplier = Math.Exp(-lambda * days_elapsed)
```

Lambda (λ) controls the speed. A larger lambda means faster fading. You pick lambda based on the question: **"After how many days should an event fade to roughly 10% of its original impact?"**

The formula is: `lambda = ln(10) / window_days`

| Window | Lambda | After the window period... |
|--------|--------|---------------------------|
| 30 days | 0.07675 | Score fades to ~10% |
| 365 days | 0.006309 | Score fades to ~10% |

Here's what that looks like — a score of 1.0 fading over 60 days with a 30-day window:

```
Score
1.0 │█
    │█
0.8 │ █
    │  █
0.6 │   █
    │    █
0.4 │     ██
    │       ██
0.2 │         ███
    │            ████
0.1 │                ██████████
    └─────────────────────────────► Days
    0    10    20    30    40    60
         ▲           ▲
         │           └── After 30 days: ~10% remains
         └── After 9 days: ~50% remains
```

The curve is the same in both domains. In Ani, it's "how much does she want to reach out?" In the monitoring system, it's "how much incident activity has this account experienced?" Same shape, same math, different interpretation.

---

## Domain 1: Ani's Desire Engine

In Ani, the desire state is a value between 0 and 1 that represents how much she wants to connect:

- **Events:** time passing, unresolved conversation threads, external triggers (news, weather)
- **Weight:** varies by trigger type — a direct prompt from an open loop weighs more than passive time drift
- **Threshold:** randomized each evaluation cycle (the key innovation — she can't predict herself)
- **Reset:** after outreach, desire drops and a sixty-minute cooldown begins

The randomized threshold is what makes this feel alive rather than mechanical. Ani might reach out at 0.62 desire or hold back until 0.81. The unpredictability means the outreach lands differently — it wasn't scheduled.

If you want the full architecture, I wrote about it in [ANI: The Architecture Behind the Companion](/Blog/Post/ani-the-architecture-behind-the-companion).

---

## Domain 2: Account Monitoring with Decay Scores

At work, the business rules were:

1. **Any incident in the last 30 days** — flag the account
2. **Any major incident (3+ hours) in the last year** — flag the account
3. **Significant accumulated impact in the last year** — even if no single incident was major, multiple smaller ones add up

Rules 1 and 3 map directly to the decay score pattern. Rule 2 needs a simple timestamp (a decay score fades too quickly to hold a single event for a full year).

### The Hybrid Approach

Each account needs five fields:

```csharp
public class AccountScores
{
    public double ShortScore { get; set; }        // 30-day decay window
    public DateTime ShortLastUpdated { get; set; }

    public double LongScore { get; set; }         // 365-day decay window
    public DateTime LongLastUpdated { get; set; }

    public DateTime? LastMajorIncidentTime { get; set; }  // Literal rule
}
```

Five fields. No historical event storage needed at runtime. O(1) per update.

### Event Weight

The weight function scales with incident duration:

```csharp
public double Weight(double hours) => 1.0 + hours / 3.0;
```

| Duration | Weight | Why |
|----------|--------|-----|
| 1 hour | 1.33 | Minor — triggers short rule only |
| 2 hours | 1.67 | Moderate — still short rule only |
| 3 hours | 2.00 | Major — meets the long threshold |
| 6 hours | 3.00 | Significant |
| 12 hours | 5.00 | Severe |

### Decision Logic

```
Short rule:  shortScore >= 1.0

Long rule (OR logic):
  - Literal:      LastMajorIncidentTime within 365 days
  - Accumulated:  longScore >= 2.0
```

The OR logic on the long rule is important. A single 3-hour incident 11 months ago still triggers (via the timestamp), even though the decay score has faded to nothing. But three 2-hour incidents in a month *also* trigger (via accumulated score), even though none individually crossed the 3-hour threshold.

### The Update Handler

```csharp
public void HandleIncidentEvent(IncidentEvent ev, AccountScores account)
{
    var eventTime = ev.EndTime;
    var weight = CalculateWeight(ev.DurationHours);

    // Update short score (30-day window)
    var daysShort = Math.Max(0, (eventTime - account.ShortLastUpdated).TotalDays);
    var newShort = (account.ShortScore * Math.Exp(-Lambda30 * daysShort)) + weight;

    if (newShort >= ShortThreshold)
        FlagAccount(ev.AccountId, "short", newShort);

    account.ShortScore = newShort;
    account.ShortLastUpdated = eventTime;

    // Update long score (365-day window)
    var daysLong = Math.Max(0, (eventTime - account.LongLastUpdated).TotalDays);
    var newLong = (account.LongScore * Math.Exp(-Lambda365 * daysLong)) + weight;

    // Update major incident timestamp
    if (ev.DurationHours >= 3.0)
        account.LastMajorIncidentTime = eventTime;

    // Check long rule (OR logic)
    bool hadMajorInLastYear = account.LastMajorIncidentTime.HasValue
        && (eventTime - account.LastMajorIncidentTime.Value).TotalDays <= 365;

    if (hadMajorInLastYear || newLong >= LongThreshold)
        FlagAccount(ev.AccountId, "long", newLong);

    account.LongScore = newLong;
    account.LongLastUpdated = eventTime;
}
```

That's the entire algorithm. Same `Math.Exp(-lambda * days)` that drives Ani's desire engine.

---

## Why the Same Math Works in Both Domains

The pattern works anywhere you need to answer the question: **"Has enough happened recently to cross a threshold?"**

Both systems share the same structural properties:

| Property | Ani's Desire Engine | Account Monitoring |
|----------|--------------------|--------------------|
| Events arrive over time | Triggers, thoughts, time passing | Service incidents |
| Events have different weights | Open loops > passive drift | 6-hour incident > 1-hour incident |
| Recent events matter more | Yesterday's thought > last week's | This week's incident > last month's |
| Score fades naturally | Desire decays toward baseline | Impact score decays toward zero |
| Threshold triggers action | Outreach when desire is high enough | Flag when score is high enough |
| Reset after action | Desire resets post-outreach | Score continues accumulating |

The only structural difference is Ani's randomized threshold — which is a domain-specific design choice about making behavior feel unpredictable, not a change to the underlying math.

---

## Edge Cases That Just Work

This is where the model earns its keep. Edge cases that would require special handling in a naive implementation just fall out naturally:

**"What if there are two incidents on the same day?"**
Decay multiplier is ~1.0 for zero elapsed time. Both weights add up. Correct behavior.

**"What if there's a burst of activity followed by months of silence?"**
The score spikes, then decays exponentially. After the window period, it's at ~10%. After twice the window, it's at ~1%. No cleanup job needed.

**"What about a brand new account with no history?"**
Score starts at 0. First event sets the score to its weight. No special initialization path.

**"What if events arrive out of order?"**
Small gaps (< 5 minutes): just add the weight to the current score. Large gaps: recompute from stored events. The decay math is commutative — order of events on the same day doesn't change the final score.

Each of these "edge cases" requires zero special handling because the model is mathematically sound. The exponential decay function is continuous, monotonic, and well-behaved at every boundary. You don't need to enumerate cases when the formula covers them all.

---

## The Takeaway

I didn't set out to build the same algorithm twice. I was solving two completely unrelated problems — one deeply personal, one purely operational — and arrived at the same place because the underlying structure of the problem was the same.

Exponential decay with weighted events and threshold evaluation is a pattern, not a solution to a specific problem. It shows up anywhere you need a "recent activity score" that fades naturally and responds to new events in constant time. I've now seen it in:

- **AI companion systems** — desire and emotional state management
- **Service monitoring** — incident impact scoring
- **Fraud detection** — transaction velocity scoring (same shape)
- **Content ranking** — "hot" scores that cool over time (Reddit, Hacker News)

The next time you find yourself writing a scheduled job that scans historical data to compute "has enough happened recently?" — stop. There's probably a decay score waiting to replace it. Five fields per entity, O(1) per update, and edge cases that just aren't.

---

*This post connects to the Ani series: [Building Ani: An AI Companion for Grief](/Blog/Post/building-ani-ai-companion-for-grief) and [ANI: The Architecture Behind the Companion](/Blog/Post/ani-the-architecture-behind-the-companion).*
