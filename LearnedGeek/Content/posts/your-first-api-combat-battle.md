# Your First Battle: A Complete API Combat Walkthrough

You've heard about [API Combat](https://apicombat.com). You're intrigued. But how do you actually **play** a game with no UI?

This is your complete walkthrough. By the end, you'll have:
- Created an account
- Recruited your first team
- Configured a strategy
- Queued your first ranked battle
- Checked the results

No previous API experience required. Just curl and curiosity.

## Prerequisites

You need:
- **curl** (comes with macOS/Linux, Windows users: Git Bash or WSL)
- **A text editor** (for saving your JWT token)
- **5 minutes**

That's it. No SDK. No downloads. Let's go.

## Step 1: Register Your Account

```bash
curl -X POST https://apicombat.com/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "YourUsername",
    "email": "you@example.com",
    "password": "SecurePass123!"
  }'
```

**Response:**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "username": "YourUsername",
  "tier": "free"
}
```

**Save that token.** You'll need it for every request.

Pro tip: Export it as an environment variable:

```bash
export API_COMBAT_TOKEN="your-token-here"
```

Now you can use `$API_COMBAT_TOKEN` in future commands.

## Step 2: Check Your Starting Roster

Every new player gets **5 starter units** automatically:

```bash
curl -X GET https://apicombat.com/api/v1/player/roster \
  -H "Authorization: Bearer $API_COMBAT_TOKEN"
```

**Response:**

```json
{
  "units": [
    {
      "id": "unit-001",
      "name": "Shield Bearer",
      "class": "Tank",
      "level": 1,
      "stats": {
        "health": 150,
        "attack": 20,
        "defense": 30,
        "speed": 8
      },
      "abilities": [
        {
          "name": "Shield Bash",
          "cooldown": 2,
          "effect": "Deals 150% attack damage and taunts enemy"
        }
      ]
    },
    {
      "id": "unit-002",
      "name": "Bronze Knight",
      "class": "Warrior",
      "level": 1,
      "stats": {
        "health": 120,
        "attack": 25,
        "defense": 20,
        "speed": 10
      },
      "abilities": [
        {
          "name": "Power Strike",
          "cooldown": 1,
          "effect": "Deals 200% attack damage to single target"
        }
      ]
    },
    {
      "id": "unit-003",
      "name": "Novice Cleric",
      "class": "Healer",
      "level": 1,
      "stats": {
        "health": 85,
        "attack": 15,
        "defense": 18,
        "speed": 12
      },
      "abilities": [
        {
          "name": "Heal Pulse",
          "cooldown": 1,
          "effect": "Restore 30 HP to lowest health ally"
        }
      ]
    },
    {
      "id": "unit-004",
      "name": "Scout",
      "class": "Ranger",
      "level": 1,
      "stats": {
        "health": 90,
        "attack": 30,
        "defense": 15,
        "speed": 20
      },
      "abilities": [
        {
          "name": "Speed Buff",
          "cooldown": 3,
          "effect": "+20% speed to all allies for 2 turns"
        }
      ]
    },
    {
      "id": "unit-005",
      "name": "Apprentice Wizard",
      "class": "Mage",
      "level": 1,
      "stats": {
        "health": 80,
        "attack": 35,
        "defense": 10,
        "speed": 15
      },
      "abilities": [
        {
          "name": "Fireball",
          "cooldown": 2,
          "effect": "AOE damage to all enemies (70% attack)"
        }
      ]
    }
  ],
  "gold": 1000,
  "battleTokens": 10
}
```

You've got:
- **1 Tank** — Shield Bearer (frontline defender)
- **1 Warrior** — Bronze Knight (high attack)
- **1 Healer** — Novice Cleric (sustain)
- **1 Ranger** — Scout (buffs/debuffs)
- **1 Mage** — Apprentice Wizard (AOE damage)

That's a full team. Let's configure them.

## Step 3: Create a Team Strategy

Strategies are JSON configurations. Here's a solid starter build:

```json
{
  "teamName": "My First Squad",
  "formation": {
    "frontLine": ["unit-001", "unit-002"],
    "backLine": ["unit-003", "unit-004", "unit-005"]
  },
  "tactics": {
    "targetPriority": "lowestHp",
    "healThreshold": 40,
    "focusFire": true,
    "useAOEWhen": 3
  }
}
```

**What this strategy does:**
- **Front line:** Shield Bearer + Bronze Knight absorb damage
- **Back line:** Novice Cleric, Scout, Apprentice Wizard stay safe
- **Target priority:** Attack weakest enemy first (finish kills fast)
- **Heal threshold:** Use Heal Pulse when ally drops below 40% HP
- **Focus fire:** All DPS attacks same target
- **AOE threshold:** Use Fireball when 3+ enemies remain

Save that to `strategy.json`, then upload it:

```bash
curl -X POST https://apicombat.com/api/v1/strategies/upload \
  -H "Authorization: Bearer $API_COMBAT_TOKEN" \
  -H "Content-Type: application/json" \
  -d @strategy.json
```

**Response:**

```json
{
  "strategyId": "strat-abc123",
  "teamName": "My First Squad",
  "status": "active",
  "winRate": null,
  "battlesPlayed": 0
}
```

Your strategy is live. Time to fight.

## Step 4: Queue a Ranked Battle

```bash
curl -X POST https://apicombat.com/api/v1/battle/queue \
  -H "Authorization: Bearer $API_COMBAT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "mode": "ranked",
    "strategyId": "strat-abc123"
  }'
```

**Response:**

```json
{
  "battleId": "battle-xyz789",
  "status": "queued",
  "estimatedTime": "30 seconds",
  "opponent": "Matchmaking..."
}
```

The matchmaking system finds an opponent of similar rank. The battle resolves automatically.

You don't have to watch. Go get coffee. Come back in a minute.

## Step 5: Check Battle Results

```bash
curl -X GET https://apicombat.com/api/v1/battle/results/battle-xyz789 \
  -H "Authorization: Bearer $API_COMBAT_TOKEN"
```

**Response (if you won):**

```json
{
  "battleId": "battle-xyz789",
  "result": "victory",
  "opponent": "EnemyPlayer42",
  "duration": 12,
  "turns": [
    {
      "turn": 1,
      "events": [
        {
          "actor": "Shield Bearer",
          "action": "Shield Bash",
          "target": "Enemy Warrior",
          "damage": 22,
          "effect": "Taunted"
        },
        {
          "actor": "Bronze Knight",
          "action": "Power Strike",
          "target": "Enemy Mage",
          "damage": 80
        }
      ]
    },
    {
      "turn": 2,
      "events": [
        {
          "actor": "Enemy Warrior",
          "action": "Attack",
          "target": "Shield Bearer",
          "damage": 10,
          "effect": "Blocked by taunt"
        },
        {
          "actor": "Novice Cleric",
          "action": "Heal Pulse",
          "target": "Shield Bearer",
          "healing": 30
        }
      ]
    }
  ],
  "summary": {
    "yourTeamSurvivors": 4,
    "enemyTeamSurvivors": 0,
    "damageDealt": 420,
    "damageTaken": 85,
    "healingDone": 90
  },
  "rewards": {
    "gold": 50,
    "rankPointsGained": 15,
    "newRank": "Rubber Duck III"
  }
}
```

**You won!**

You earned:
- **50 gold** (use to recruit more units)
- **15 rank points** (climb the leaderboard)
- **Promoted to Rubber Duck III** (from Rubber Duck IV)

## Step 6: Analyze and Optimize

Look at the turn-by-turn breakdown. What worked?

- **Shield Bash** taunted the enemy warrior → enemy wasted turns attacking your Shield Bearer
- **Power Strike** one-shot their mage → removed their main threat
- **Heal Pulse** kept your Shield Bearer alive → sustained frontline

What could improve?

- Your Scout didn't use Speed Buff (conditions weren't met)
- AOE wasn't triggered (only started with 5 enemies, threshold was 3)

Adjust your strategy:

```json
{
  "tactics": {
    "targetPriority": "highestThreat",
    "healThreshold": 50,
    "focusFire": true,
    "useAOEWhen": 4,
    "useSpeedBuffTurn": 1
  }
}
```

Changes:
- Target highest threat first (kill mages/healers early)
- Heal at 50% HP (more aggressive sustain)
- AOE when 4+ enemies (save mana for critical moments)
- Use Speed Buff turn 1 (faster team = more actions)

Upload the new strategy. Queue another battle. Repeat.

## What's Next?

**Recruit More Units**

You earned 50 gold. Recruit your 6th unit:

```bash
curl -X GET https://apicombat.com/api/v1/player/roster/available \
  -H "Authorization: Bearer $API_COMBAT_TOKEN"
```

Browse available units. Spend gold to recruit. Diversify your roster.

**Build More Strategies**

Free tier allows **3 team slots**. Create different builds:
- **Aggressive:** All DPS, no healer, rush strategy
- **Defensive:** Double tank, healer focus, outlast opponent
- **Hybrid:** Balanced, adapts to any matchup

**Track Your Rank**

```bash
curl -X GET https://apicombat.com/api/v1/leaderboard?limit=10 \
  -H "Authorization: Bearer $API_COMBAT_TOKEN"
```

See where you rank. Watch your climb from Rubber Duck to I Use Arch btw.

**Build a Client**

Playing via curl is fun for learning. But you'll want automation.

Build:
- **Python bot** that auto-battles 10 times daily
- **Web dashboard** to visualize your roster/stats
- **Discord bot** that notifies you of battle results
- **CLI tool** for easier team management

That's the real game. The API is your playground.

## Free vs Premium

Free tier gives you:
- **10 battles/day** (plenty for learning)
- **All 25 units** (purchased with in-game gold)
- **3 team slots** (test different builds)
- **Batch practice** (up to 200 simulated battles)

Premium ($5/month) unlocks:
- **Unlimited battles** (iterate faster)
- **Guild creation** (team play)
- **Player analytics** (track your progress)

Worth it if you're serious. But free tier is genuinely playable.

## You Just Played a Game With No UI

Think about what you did:

1. Registered via API
2. Retrieved data via API
3. Configured strategy via JSON
4. Triggered gameplay via API
5. Analyzed results via API

That's the entire game loop. No graphics. No buttons. Just HTTP requests.

And it was... kind of fun, right?

Now imagine building a dashboard that visualizes this. Or a bot that optimizes your strategy using machine learning. Or a guild coordination tool that shares strategies.

That's API Combat. The API is the beginning, not the end.

---

**Ready for battle #2?** Queue another ranked match. Try a different strategy. See how high you can climb.

**Want automation?** Check out my next post: *Building Your First API Combat Bot in Python*.

**Educators:** Want to use this tutorial in your classroom? [Contact me](https://learnedgeek.com/Contact) for Education Mode access.

Go queue your next battle: [apicombat.com](https://apicombat.com)

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Building a Turn-Based Battle Engine in 400 Lines of C#](/Blog/Post/battle-engine-400-lines-csharp) for what happens under the hood when your battle resolves, [One Battle, Ten Service Calls: Fan-Out Without a Message Bus](/Blog/Post/fan-out-without-message-bus) for the post-battle cascade that updates your rank and rewards, and [Environmental Modifiers: Weekly Meta Rotation](/Blog/Post/environmental-modifiers-weekly-rotation) for the weekly rule changes that keep battles fresh.*

---

*Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>*
