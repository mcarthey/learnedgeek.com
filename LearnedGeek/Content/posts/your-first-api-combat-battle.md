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
curl -X POST https://api.apicombat.com/v1/auth/register \
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
curl -X GET https://api.apicombat.com/v1/player/roster \
  -H "Authorization: Bearer $API_COMBAT_TOKEN"
```

**Response:**

```json
{
  "units": [
    {
      "id": "unit-001",
      "name": "Rookie Tank",
      "class": "Tank",
      "level": 1,
      "stats": {
        "health": 120,
        "attack": 15,
        "defense": 30,
        "speed": 10
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
      "name": "Starter Warrior",
      "class": "DamageDealer",
      "level": 1,
      "stats": {
        "health": 80,
        "attack": 40,
        "defense": 15,
        "speed": 20
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
      "name": "Field Medic",
      "class": "Healer",
      "level": 1,
      "stats": {
        "health": 70,
        "attack": 10,
        "defense": 12,
        "speed": 15
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
      "class": "Support",
      "level": 1,
      "stats": {
        "health": 65,
        "attack": 20,
        "defense": 10,
        "speed": 30
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
      "name": "Apprentice Mage",
      "class": "Specialist",
      "level": 1,
      "stats": {
        "health": 60,
        "attack": 35,
        "defense": 8,
        "speed": 18
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
- **1 Tank** (frontline defender)
- **1 Damage Dealer** (high attack)
- **1 Healer** (sustain)
- **1 Support** (buffs/debuffs)
- **1 Specialist** (AOE damage)

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
- **Front line:** Tank + Warrior absorb damage
- **Back line:** Healer, Support, Mage stay safe
- **Target priority:** Attack weakest enemy first (finish kills fast)
- **Heal threshold:** Use Heal Pulse when ally drops below 40% HP
- **Focus fire:** All DPS attacks same target
- **AOE threshold:** Use Fireball when 3+ enemies remain

Save that to `strategy.json`, then upload it:

```bash
curl -X POST https://api.apicombat.com/v1/strategy/create \
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
curl -X POST https://api.apicombat.com/v1/battle/queue \
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
curl -X GET https://api.apicombat.com/v1/battle/result/battle-xyz789 \
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
          "actor": "Rookie Tank",
          "action": "Shield Bash",
          "target": "Enemy Warrior",
          "damage": 22,
          "effect": "Taunted"
        },
        {
          "actor": "Starter Warrior",
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
          "target": "Rookie Tank",
          "damage": 10,
          "effect": "Blocked by taunt"
        },
        {
          "actor": "Field Medic",
          "action": "Heal Pulse",
          "target": "Rookie Tank",
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

- **Shield Bash** taunted the enemy warrior → enemy wasted turns attacking your tank
- **Power Strike** one-shot their mage → removed their main threat
- **Heal Pulse** kept your tank alive → sustained frontline

What could improve?

- Your support unit didn't use Speed Buff (conditions weren't met)
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
curl -X GET https://api.apicombat.com/v1/shop/units?class=Tank \
  -H "Authorization: Bearer $API_COMBAT_TOKEN"
```

Browse available units. Spend gold. Diversify your roster.

**Build More Strategies**

Free tier allows **3 strategy slots**. Create different builds:
- **Aggressive:** All DPS, no healer, rush strategy
- **Defensive:** Double tank, healer focus, outlast opponent
- **Hybrid:** Balanced, adapts to any matchup

**Track Your Rank**

```bash
curl -X GET https://api.apicombat.com/v1/leaderboard?limit=10 \
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
- **20 units unlocked** (good variety)
- **3 strategy slots** (test different builds)

Premium ($5/month) unlocks:
- **Unlimited battles** (iterate faster)
- **All 50+ units** (meta optimization)
- **Guild access** (team play)
- **Simulation endpoint** (test 10K battles instantly without queueing)

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

*Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>*
