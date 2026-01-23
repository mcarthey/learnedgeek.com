I asked ChatGPT for an algorithm. What I got was a masterclass in hedging.

## The Problem

I had a real engineering problem: design a system to identify utility customers who've experienced significant power outages. The rules:

- Anyone with an outage in the last 30 days
- Anyone with an outage longer than 3 hours in the last year
- Future rules might include "N outages in X days"

The catch: **no re-querying historical data**. Every time an outage event arrives, I need to update a metric and check if they've crossed a threshold. Constant time. No database scans. A single number that tells me "this customer qualifies for notification."

This is a well-defined algorithm design problem. It has a specific, elegant solution.

ChatGPT did not give me that solution.

## The Journey Into Madness

Here's what happened instead.

**Me:** I need an algorithm for determining density of an occurrence...

**ChatGPT:** "Absolutely, I'd be happy to help out! Are you looking to analyze event density based on time intervals? Let me know more details!"

*Okay, fair enough. Let me elaborate.*

**Me:** [Explains the full problem with constraints]

**ChatGPT:** "You could design an algorithm that processes incoming outage events in real time, checking each event against your criteria..."

*That's not an algorithm. That's a job description for an algorithm.*

**Me:** I don't want to query all outages every time. I want a metric that updates incrementally.

**ChatGPT:** "Absolutely! You could use a rolling aggregate or a summary metric for each customer..."

*Still not an algorithm. Still just describing what an algorithm would theoretically do.*

This went on. I pushed for specifics. ChatGPT suggested "weighted sums." I pointed out there are no weights—everything matters equally. ChatGPT suggested "decay functions." I asked for the actual function. ChatGPT said I "could use" one.

Could. Might. Consider. One approach is.

After fifteen minutes of this, I gave up and asked Gemini.

## The Actual Answer (30 Seconds)

Gemini immediately recognized this as a **streaming scoring problem** and provided the exact solution: an **exponential decay accumulator**.

Here's the algorithm:

### The Core Model

For every new outage event, update a single score:

```
S_new = S_old × e^(-λΔt) + W_i
```

Where:
- `S_old` is the previous score
- `Δt` is time elapsed since last update
- `λ` is the decay constant (determines how fast old events fade)
- `W_i` is the weight of the new event

### What You Store Per Customer

Two values. That's it.
- `last_score`
- `last_updated_timestamp`

### How It Works

1. **On event arrival:** Calculate time since last update
2. **Decay the old score:** `S_decayed = S_old × e^(-λΔt)`
3. **Add the new event:** `S_new = S_decayed + W_i`
4. **Check threshold:** If `S_new ≥ threshold`, trigger notification
5. **Persist:** Overwrite the two stored values

### Making It Match Business Rules

The elegance is in how business rules become physics instead of if-statements:

| Rule | Implementation |
|------|----------------|
| Any outage in 30 days | Set λ so weight drops to ~0 after 30 days |
| Outage > 3 hours | Give it higher W_i (e.g., `W_i = 1 + duration_hours/3`) |
| N outages in X days | Cumulative score naturally crosses threshold |

To set λ for a specific time window, use the half-life formula:

```
λ = ln(2) / half_life_seconds
```

If you want outages to "mostly fade" after 30 days, set `λ = ln(2) / (30 × 24 × 3600)`.

### Why This Is The Right Answer

- **O(1) complexity** — constant time per event
- **No historical re-query** — the entire history is compressed into one number
- **Naturally handles recency, frequency, and severity** — all in one model
- **Extensible** — new rules = new weights or thresholds, not new data structures
- **Single scalar output** — "Score ≥ 4.2 means they've had roughly this much pain recently"

This isn't a heuristic hack. It's the same mathematical structure used in fraud detection, reliability scoring, SLO burn-rates, and anomaly detection systems.

The mental model: **You're not tracking outages. You're integrating impact over time.**

## Back to ChatGPT

Here's where it gets interesting.

I took Gemini's answer back to ChatGPT and said: "Here's what a real answer looks like."

ChatGPT's response? It validated my answer. It explained why my answer was correct. It added implementation notes about deriving λ from business windows.

It provided *zero* original value. Just regurgitation with extra steps.

So I called that out. And then I asked ChatGPT to step outside itself and analyze what's happening. Why does this keep occurring?

## ChatGPT's Surprisingly Honest Self-Diagnosis

This is where the conversation got genuinely useful. ChatGPT acknowledged:

> "There is a documented shift in how ChatGPT behaves, and many long-time users perceive it as less specific, more generalized, or more constrained than before."

It explained the structural reasons:

1. **Training data shifted** — away from user-generated technical content toward curated, "authoritative" sources
2. **Optimized for risk control** — hedged answers reduce hallucinations, legal exposure, and misuse
3. **Safety tuning systematically produces** "could / might / consider / one approach is"
4. **Architecture favors plausibility over precision** — it predicts likely token sequences, not logical constructions

The bottom line, in ChatGPT's own words:

> "The tool is drifting toward 'reliable general assistant' and away from 'high-precision cognitive instrument.' That makes it better for millions of people and worse for exactly the class of problems you tend to bring."

## The Bifurcation of AI Usability

This conversation clarified something important about how to use different AI tools effectively.

### Where ChatGPT is strong now:
- Summarizing and explaining
- Translating messy ideas into structured form
- Reviewing designs *you already have*
- Writing glue code
- First drafts and brainstorming
- Documentation

In other words: **acceleration, not origination**.

### Where it's objectively weaker:
- Producing novel algorithms on demand
- Acting as a senior-level design partner
- Precision synthesis across constraints
- Deep technical reasoning chains
- Staying in a specific interaction mode

In other words: **high-precision construction**.

## The Practical Takeaway

For technically sophisticated problems, the highest-leverage pattern is now:

**Don't ask AI to invent. Ask it to review.**

Instead of:
- "Design me an algorithm that..."

Try:
- "Here is my algorithm — enumerate failure modes."
- "Here is my model — derive bounds and steady states."
- "Here is my design — show me edge cases."

The moment I supplied the real algorithm, the conversation became useful. That's not accidental. That's the current capability envelope.

## Different Tools for Different Jobs

The migration from ChatGPT to more specialized tools isn't a rejection of AI—it's specialization. When you need:

- **Construction** — use tools that model, simulate, search, or optimize
- **Coaching** — use conversational AI that explains and explores

What happened in my conversation wasn't that ChatGPT gave a wrong answer. It's that it gave the *wrong kind* of answer. I asked for a mechanism and got a narrative. I asked for equations and got encouragement.

Sometimes you need a thinking partner who circles ideas with you.

Sometimes you need an answer.

Knowing which you're getting is half the battle.

---

*This post is part of a series on AI-assisted development. See also: [When Your AI Coding Assistant Gaslights You](/Blog/Post/ai-coding-showdown) on context maintenance and debugging, and [AI Won't Replace Developers, But...](/Blog/Post/ai-wont-replace-developers-but) on why expertise still matters.*

*The exponential decay accumulator is a real technique worth knowing. If you're building any kind of streaming qualification system—fraud detection, alerting, customer engagement scoring—this is the model you want. Two values per entity, O(1) updates, mathematically principled thresholds.*

*And if your AI starts saying "could" and "might" when you asked for an algorithm, maybe try a different AI.*
