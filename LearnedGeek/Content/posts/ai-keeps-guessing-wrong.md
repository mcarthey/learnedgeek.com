"The API isn't syncing data."

That was the report during a client demo. Android app on one side, web backend on the other, and somewhere between them, silence. No data flowing. A room full of stakeholders. The clock ticking.

So the developer fired up Claude and asked for help. First answer: "The API route is probably wrong." Plausible. Changed it. Still broken. Second answer: "It's likely an auth issue." Also plausible. Tweaked the auth config. Still broken. Third answer: "Maybe it's a serialization mismatch." Sure, why not. Changed the DTO. Still broken.

Three fixes. Three confident explanations. Zero progress. The demo ended with that special flavor of awkwardness where everyone pretends the dead silence is fine.

Here's the thing — none of those answers were *wrong*. They were all reasonable guesses. But they were **guesses**. And guessing, no matter how intelligent, is not debugging.

## The Confident Guess Spiral

I've seen this enough times now to name it:

1. You describe a symptom: "It doesn't work."
2. The AI proposes the most statistically likely cause.
3. You try the fix. It doesn't help.
4. The AI proposes the second most likely cause.
5. You try that. Nope.
6. Repeat until you run out of time, patience, or both.

Each guess sounds reasonable in isolation. The AI isn't hallucinating — it's pattern-matching against every Stack Overflow post and GitHub issue it's ever seen. But pattern-matching without evidence is just sophisticated coin-flipping.

The problem isn't the AI. The problem is that **nobody gave it the evidence it needed**.

I know this because I spent about two weeks and 50+ commits learning it the hard way across two different projects.

## Three Questions That Kill The Spiral

Every debugging dead end I hit during that stretch could have been short-circuited with the same three questions:

**1. What is the HTTP status code?**

This alone eliminates 80% of guesses. A 401 is not a 404 is not a 500. Each one points to a completely different part of the stack. If the AI is choosing between "wrong route" and "auth issue," the status code answers it instantly.

**2. What is in the response body?**

The server almost always tells you what went wrong — if you actually read the response. A `{ "error": "Token expired" }` on a 401 is a different fix than `{ "error": "Invalid signature" }` on the same 401. One means the user needs to re-login. The other means your JWT secret doesn't match between environments.

**3. Does the request match the API spec?**

Wrong route, wrong HTTP method, wrong content type, wrong field name in the body. Compare the actual request against the OpenAPI spec. If the spec says `POST /api/v1/battles` and the client is sending `POST /api/battles`, that's your bug. No code reading required.

Three questions. Under a minute. No guessing.

If you *can't* answer these three questions, no amount of code reading will help. You need to capture the actual traffic first.

## How I Got Here (The Painful Part)

I didn't arrive at this by being smart. I arrived at it by being burned — repeatedly — while hardening [API Combat](https://apicombat.com) for production.

The demo failure was the wake-up call, but the real education was the two weeks that followed. Every time something broke, I asked myself: "Why didn't I catch this sooner?" And the answer was always the same — I didn't have the evidence I needed, and I was too busy guessing to go get it.

So I started building systems that make evidence automatic:

**Structured error responses** replaced stack traces and HTML error pages. Every error now returns JSON with a human-readable message and a support ID (format: `ERR-XXXX-XXXX`). User reports the ID, I find the exact error in seconds. No more "can you describe what happened?" conversations.

**An audit trail** across 46 points in 17 services records every state change. When something goes wrong, I can reconstruct exactly what happened, in what order, by whom. I'm reading history, not guessing at it.

**Auth scheme documentation** got explicit. We run three auth schemes (API keys, JWT, cookies) and the dual-scheme gotcha — browser JavaScript calling API endpoints needs `[Authorize(AuthenticationSchemes = "Cookies,Bearer")]` — is now documented, tested, and has its own checklist. Because I got burned by it. Twice.

**A robot player** walks every HATEOAS link in the API without using any internal DTOs. If a route changes or a response shape drifts, the test breaks. The OpenAPI spec is the single source of truth, enforced automatically.

## The Prompt That Changed Everything

All of this boils down to one principle: every request should either **succeed with the expected response**, or **fail with a clear, logged, diagnosable error**.

No silent failures. No ambiguous states. No "it didn't work and I don't know why."

When your system works like this, debugging with AI actually becomes useful. Not because the AI got smarter, but because you can finally give it what it needs:

**Before:**

> "The app isn't syncing data."

This produces three rounds of guessing and a wasted afternoon.

**After:**

> "POST /api/v1/battles returns 401 with body `{ "error": "Unauthorized" }`. The request includes `Authorization: Bearer <token>`. The token was issued 2 hours ago. JWT expiry is set to 60 minutes."

This produces a diagnosis in one shot: "Your token expired. A token from 2 hours ago is invalid with 60-minute expiry. The client needs to refresh before retrying."

Same AI. Same model. Completely different outcome. The difference is evidence.

## The Part I Didn't Expect

After documenting all of this into a hardening playbook and testing strategy, I handed them to a *different* Claude instance working on a completely different project — a field crew management app with a Blazor/MAUI stack.

That project had its own testing problems. A previous AI session had implemented shell-based database testing using CLI tools on the CI runner (which only existed inside the Docker container, not on the runner host). When that failed, the AI "fixed" it by calling the database migration directly with the wrong schema. Each fix cascaded into another problem.

Classic confident guess spiral.

After pointing the new Claude instance at the testing strategy document, the first thing it said was: *"This old plan includes the Tier 3 shell-based CI smoke test which is exactly the anti-pattern documented in TESTING-STRATEGY.md. I need to replace this entirely."*

It self-corrected. Not because it was smarter, but because it had the right context. It read the documented lessons, recognized the pattern it was about to repeat, and chose a different path.

That was the moment I realized: the lessons don't just help *me* debug faster. They help *every AI session* on *every project* avoid the same mistakes.

## Making It Stick

So I built a system for it:

A **global CLAUDE.md** at `~/.claude/CLAUDE.md` that every Claude session loads automatically across all projects. It contains the universal rules: don't guess without evidence, don't bypass existing infrastructure, stop after two failed fixes and reassess.

**Project-specific playbooks** in `docs/` that document what worked, what didn't, and why. Each project's CLAUDE.md references these.

A **"Contributing Back" section** in the global file that invites every Claude instance to propose updates based on what it learns during a session. Every project's hard-won lessons feed back into the global file. Every new project starts smarter than the last.

It's not magic. It's just institutional memory that actually persists — something that's weirdly hard to achieve with AI assistants that start fresh every conversation.

## The Demo Survival Kit

If you take one thing from this post, make it this. Before your next demo:

- **Health endpoint open in a browser tab.** Confirms the server is alive at a glance.
- **Server logs open in a second window.** See errors in real-time.
- **A network capture tool ready** (Chrome DevTools, Fiddler, Charles Proxy). When something breaks, you have the evidence immediately.
- **API docs bookmarked.** Verify endpoint contracts on the spot.

And when someone says "it's broken" — whether that someone is a human or an AI — don't guess. Ask:

1. What's the status code?
2. What's in the response body?
3. Does the request match the spec?

Three questions. Every time. It works.

## See For Yourself

The full hardening playbook (19 sections, from auth gotchas to emergency triage flowcharts) and the testing strategy document (18 sections, including anti-patterns and a quick reference for AI assistants) are both available in the [API Combat repository](https://github.com/mcarthey/ApiCombatGame).

They're written to be handed to any AI assistant on any project. If you've been burned by the confident guess spiral, they might save you some scars.

And if you want to see the system they were built to protect — come play.

**Try it:** [apicombat.com](https://apicombat.com)
**Read the docs:** [apicombat.com/api-docs/v1](https://apicombat.com/api-docs/v1)
**Source:** [github.com/mcarthey/ApiCombatGame](https://github.com/mcarthey/ApiCombatGame)

---

*This post is part of a series about building API Combat. See also: [Why I Built a Game With No GUI](/Blog/Post/why-i-built-a-game-with-no-gui) and [Thread-Safe Synchronization with SemaphoreSlim](/Blog/Post/semaphore-thread-safe-sync).*
