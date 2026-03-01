# Why I Built a Game With No GUI

"So it's like... a text-based game?"

"No."

"A terminal game?"

"Not exactly."

"Then what do you *see* when you play?"

"JSON."

That's the conversation I've had a dozen times explaining [API Combat](https://apicombat.com). And every time, I watch the same reaction: confusion, then curiosity, then—if they're a developer—a smile.

Because they *get it*.

## The Problem With Most Games

Every game I've played in the last decade follows the same formula:

1. Download a client
2. Create an account (through the client)
3. Click buttons
4. Watch animations
5. Click more buttons

The UI **is** the game. Interaction is limited to what the developers designed.

Want to automate repetitive tasks? Too bad.
Want to analyze your stats programmatically? Too bad.
Want to build custom dashboards? Too bad.

You're trapped in their interface.

As a developer, this drives me insane.

## The API-First Philosophy

I've spent 20 years building APIs. I love them. Not because they're trendy. Because they're **composable**.

An API says: "Here's the data. Here's what you can do with it. Build whatever you want."

A UI says: "Here's the *only* way to interact with this data."

APIs empower. UIs constrain.

So I asked: What if a game was API-first?

Not "has an API for power users." Not "optional API alongside the main UI."

What if the API **was** the game?

## What That Looks Like

In API Combat:

- Your "login screen" is a POST request to `/api/v1/auth/login`
- Your "main menu" is a GET request to `/api/v1/player/profile`
- Your "battle button" is a POST request to `/api/v1/battle/queue`
- Your "results screen" is a GET request to `/api/v1/battle/results/{id}`

There's a web UI for account management and browsing the docs, but all **gameplay** happens through the API. No battle screen. No drag-and-drop team builder. Just endpoints.

Want to check your roster? `curl` it.
Want to configure a strategy? POST a JSON file.
Want to see the leaderboard? GET `/api/v1/leaderboard`.

The entire game is over 100 documented API endpoints covering battles, teams, guilds, tournaments, loot, and a battle pass. That's it.

## Why This Is Better

### 1. You Build What You Want

Some players use curl. Some use Postman. Some write Python bots. Some build full web dashboards.

There's a [Python starter client](https://github.com/api-combat-game/python-starter) that plays through the full game loop in about 250 lines of code. But that's just a starting point.

Imagine a Discord bot that lets your guild queue battles from chat. A Grafana dashboard that visualizes your win rate trends. A machine learning model that optimizes strategies based on the meta.

**I didn't build any of that.** The API lets you.

### 2. Automation Is Part of the Game

Most games punish automation (macros = ban). API Combat **encourages** it.

You can write scripts that:
- Auto-queue battles based on conditions
- Switch strategies dynamically
- Analyze opponent patterns
- Coordinate guild attacks

The best players aren't just good at strategy. They're good at **code**.

### 3. Learning Feels Like Play

I've taught hundreds of developers. The hardest part isn't explaining REST or JSON. It's making them **care**.

"Build a todo API" is educational. But it's boring.

"Build a combat bot that battles your classmates" is **engaging**.

Students who build API Combat clients learn:
- HTTP methods (GET, POST, PUT, DELETE)
- Authentication (JWT tokens, headers)
- JSON parsing and serialization
- Rate limiting and error handling
- Async programming (battle results aren't instant)
- Strategy design through code

They learn all that **while trying to win battles**. Not because I told them to. Because they want to climb the leaderboard.

### 4. No UI = No UI Bugs

I've shipped games before. You know what eats 80% of dev time?

The UI.

Button alignment. Responsive layouts. Browser compatibility. Mobile vs desktop. Dark mode. Accessibility.

API Combat has **zero gameplay UI**. Which means:
- No CSS bugs
- No browser quirks
- No mobile-specific issues
- No design debates

I shipped a full game with 100+ endpoints. Alone. Because I wasn't spending months on UI.

### 5. It's Developer Catnip

Developers love problems. Not generic problems. **Their** problems.

"How do I optimize this strategy?" is a problem they want to solve.

"How do I build a bot that auto-battles while I sleep?" is a problem they want to solve.

"How do I coordinate 20 guild members in a synchronized attack?" is a problem they want to solve.

I didn't create a game. I created a **playground for developer problems**.

And developers love playgrounds.

## What I Learned Building This

### 1. Documentation IS the Game

With no UI, documentation isn't nice-to-have. It's **critical**.

I spent more time on API docs than on some features. OpenAPI spec. Example requests. Error code explanations. Full endpoint documentation organized by feature.

Because if the docs suck, the game is unplayable.

Turns out, this is a good thing. It forced me to design clean APIs. Clear naming. Consistent patterns.

The game is better because the docs had to be good.

### 2. Async Gameplay Changes Everything

Most games require presence. You can't play Fortnite "while you sleep."

API Combat is async-first. You configure a strategy. Queue a battle. Come back later.

This means:
- Global audience (timezones don't matter)
- Casual-friendly (busy developers can compete)
- Strategic depth (planning > reflexes)

The best players aren't the ones who play the most. They're the ones who **think** the most.

### 3. Removing UI Removes Barriers

I thought "no GUI" would scare people away. It did the opposite.

The concept clicks immediately with developers. Players who've never touched Postman learn it for API Combat. Students who've never written Python write their first script to auto-battle.

Removing the UI didn't make it harder. It made it **theirs**.

### 4. Developers Will Surprise You

This is the part I'm most excited about. The game is designed so players can build anything on top of it. Custom dashboards. Bots. Automated analysis tools. Guild coordination systems.

I gave them APIs. What they build with them is up to them.

## Why This Won't Work for Most Games

Let me be clear: This approach isn't universal.

API-first works for API Combat because:
- The audience is developers (they use APIs daily)
- The gameplay is turn-based (async = API-friendly)
- Strategy > visuals (graphics aren't needed)
- Automation is core (not a cheat, a feature)

This wouldn't work for:
- Platformers (real-time input required)
- Visual novels (graphics are the point)
- MMORPGs (massive state, complex UI needed)
- Casual mobile games (non-technical audience)

API Combat works **because it's designed for developers**.

## The Real Reason I Built This

Here's the truth:

I was tired of boring API tutorials.

I was tired of students glazing over during REST lessons.

I was tired of building CRUD apps that no one cares about.

I wanted to build something that made developers **excited** to call an API.

Something where the first curl command feels like casting a spell.

Something where writing a POST request feels like declaring war.

Something where parsing JSON feels like opening a loot box.

I wanted API consumption to feel like **play**.

So I removed everything that wasn't the API.

And what's left is the purest form of developer gameplay I've ever built.

## Come Play

API Combat is live.

Maybe developers want UIs. Maybe "just APIs" is too weird. Maybe I've built something only I find fun.

Or maybe there's an audience of developers who've been waiting for a game that treats them like developers.

Who want to build their own clients.
Who want to automate their gameplay.
Who want strategy expressed in JSON, not menu screens.

If that's you, I built this for you.

Come play. Or don't play. Build something. Break something. Automate something.

The API is live. The game is yours.

---

**Try it:** [apicombat.com](https://apicombat.com)

**Read the docs:** [apicombat.com/api-docs/v1](https://apicombat.com/api-docs/v1)

**Get started fast:** [Python starter client](https://github.com/api-combat-game/python-starter) | [Bruno API collection](https://github.com/api-combat-game/bruno-collection)

**Join the community:** [Discord](https://discord.gg/jfSCSfAN49) | [GitHub](https://github.com/api-combat-game)

---

*This post is part of a series about building [API Combat](https://apicombat.com). See also: [Building a Turn-Based Battle Engine in 400 Lines of C#](/Blog/Post/battle-engine-400-lines-csharp) for the engine that powers every battle, [Hand-Rolling Rate Limiting by Subscription Tier](/Blog/Post/rate-limiting-by-subscription-tier) for how we handle tier-based API throttling, and [10 Background Jobs on Shared Hosting](/Blog/Post/background-services-shared-hosting) for how we keep everything running behind the scenes.*
