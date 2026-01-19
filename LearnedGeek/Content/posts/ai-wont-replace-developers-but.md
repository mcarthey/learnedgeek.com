## The LinkedIn Hot Take Industrial Complex

My feed is full of them lately: "AI will replace all developers!" "Learn prompting, not programming!" "Why I fired my dev team and hired ChatGPT!"

The comments are predictable. Half agree enthusiastically (usually people selling AI courses). Half disagree defensively (usually developers worried about their jobs). Both miss the point.

I've spent the last year building production applications with AI assistance. Not toy projects—real sites handling real users, real payments, real data. A Spanish language learning platform with Stripe subscriptions. A Discord bot summarizing hundreds of messages. A data lake architecture for analyzing my own AI conversations. A nonprofit foundation site. This very blog.

Here's what I actually learned: **AI won't replace developers, but it will replace developers who can't direct AI effectively.**

And "directing AI effectively" requires something the hot takes never mention: you need to actually know what you're doing.

## The Myth of the Zero-Knowledge Developer

The fantasy goes like this: You describe what you want. AI writes the code. You copy-paste. Ship it. Success!

Here's what actually happens:

**You:** "Build me a user authentication system."

**AI:** *Generates 200 lines of code*

**You:** *Copy-paste into project*

**Your project:** *Catches fire*

The code might work in isolation. It might even be good code. But you don't know:

- Whether it fits your existing architecture
- What security assumptions it's making
- How it handles edge cases
- What dependencies it introduced
- Whether it follows your project's patterns
- If it's even solving the right problem

I watched AI generate a beautiful payment webhook handler. It was clean, well-documented, properly typed. It also assumed synchronous database transactions in an async context, which would have caused race conditions under load. I caught it because I've debugged race conditions before. A "zero-knowledge developer" would have shipped it and wondered why payments randomly failed.

## What AI Actually Does Well

Let me be fair. AI has genuinely transformed my workflow:

### Large Refactoring Operations

Renaming a method across 40 files? Updating an interface and all its implementations? Converting a codebase from one pattern to another? These tasks are tedious, error-prone for humans, and perfect for AI.

Recently I restructured how blog posts work in this site. AI handled the mechanical parts—updating every reference, adjusting every test, fixing every import. What would have been a day of careful find-and-replace was done in minutes.

### Creating Assets I Can't Create Myself

Every blog post on this site has a custom SVG illustration. I don't have graphic design skills. I don't own Illustrator. But I can describe what I want and iterate on the result.

AI generates the SVG code directly. I can inspect it, adjust colors, tweak positioning. The result is custom artwork that matches my content perfectly—something I couldn't create before at any price point.

### Writing Commit Messages

This sounds trivial, but it matters. Good commit messages explain *why*, not just *what*. AI reads the diff, understands the context from our conversation, and writes messages that future developers (including future me) will thank me for.

Before: `git commit -m "fix bug"`

After: `git commit -m "Fix N+1 query in conversation loading causing timeout under load"`

### Boilerplate and Scaffolding

New controller? New service class? Standard test structure? AI generates it faster than I can type, following the patterns already established in the codebase.

## What AI Cannot Do (Yet)

Here's where the hot takes fall apart:

### Architectural Decisions

When I built [ChatLake](https://github.com/mcarthey/ChatLake), I chose a Bronze/Silver/Gold data lake architecture. Why? Because I need immutability for data lineage. Because I want to replay transformations. Because ML.NET inference works better on denormalized data.

AI didn't make those decisions. AI doesn't know my requirements, my constraints, my future plans. It can implement whatever architecture I choose, but choosing the right architecture requires understanding tradeoffs that span the entire system.

### Security Boundaries

[Lake Country Spanish](https://github.com/mcarthey/lakecountryspanish.com) handles payments through Stripe. It has role-based access control for students, teachers, and administrators. It stores subscription data.

Every boundary between these systems is a security decision. Should the webhook have access to user data? Which roles can modify class schedules? How long should session tokens live? These aren't coding problems—they're threat modeling problems. AI can implement the pattern I choose; it can't know which threats matter for my specific use case.

### Integration Strategy

[TLDRkseid](https://github.com/mcarthey/Discord-TLDRkseid) integrates Discord, OpenAI, and SQLite. Each integration point required decisions:

- How aggressively to cache to avoid API costs?
- How to handle rate limits from both Discord and OpenAI?
- What to do when the OpenAI API is slow or unavailable?
- How to structure data for efficient retrieval?

These decisions emerged from understanding how each system works, how users will interact with the bot, and what failure modes matter. AI helped implement the solutions. It couldn't design the overall integration strategy.

### Debugging Judgment

When something breaks, you need judgment about where to look first. Is it a data problem? A race condition? A configuration issue? An infrastructure problem?

I spent days debugging a Discord bot that had stopped receiving events. AI couldn't help because the symptom ("bot doesn't respond") has a hundred possible causes. It took human judgment to eventually discover that the Discord *application itself* had become corrupted—something no amount of code inspection would reveal.

## The Real Workflow

Here's what my AI-assisted development actually looks like:

**1. I design the solution.**
What are we building? Why? What constraints exist? What patterns fit?

**2. I direct the implementation.**
"Create a service that validates reCAPTCHA tokens. Use the existing HttpClient factory pattern. Return a result object with success status and score."

**3. AI writes the first draft.**
Usually 80-90% correct. Sometimes perfect. Occasionally completely wrong.

**4. I review everything.**
Does this fit our patterns? Is it secure? Does it handle edge cases? Will it scale? Is it even correct?

**5. I refine with AI's help.**
"The score comparison should use >= not >." "Add logging for failed validations." "This needs to handle null tokens."

**6. I verify it works.**
Write tests. Run them. Try edge cases manually. Deploy to staging. Test again.

This isn't "AI writing code while I watch." It's a collaboration where my expertise shapes every decision, and AI accelerates the mechanical parts.

## The reCAPTCHA Story

Here's a concrete example from last week.

My wife couldn't submit the contact form on our website. From her phone. On our couch. reCAPTCHA flagged her as a bot with a score of 0.1.

AI didn't catch this during implementation. Why would it? The code worked. The threshold was set to Google's recommended 0.5. Everything looked fine.

But I understand that:
- Mobile users have less browsing history for Google to analyze
- Touch interfaces don't generate mouse movement data
- Mobile carriers use NAT, sharing IP addresses among thousands of users
- VPN users (like my privacy-conscious wife) share exit nodes

Knowing this, I could diagnose the problem and choose a solution: lower the threshold to 0.05 and rely on defense-in-depth (honeypot + anti-forgery + reCAPTCHA together).

AI implemented the fix in seconds. But identifying that we needed a fix, understanding why, and choosing the right solution? That required years of experience with web applications, security, and user behavior.

## The Skill Shift

AI doesn't eliminate the need for skill. It shifts which skills matter.

**Less important:**
- Memorizing syntax
- Writing boilerplate
- Recalling API signatures
- Typing speed

**More important:**
- System design and architecture
- Security threat modeling
- Understanding failure modes
- Debugging intuition
- Code review skills
- Knowing when AI is wrong

That last one is crucial. AI is confidently wrong all the time. If you can't recognize incorrect code, you'll ship incorrect code. The bugs will be subtle, the security holes will be invisible, and you'll have no idea why things break in production.

## For the Hot Take Crowd

To the "AI replaces developers" crowd: You're selling a fantasy. AI is a powerful tool, not a replacement for understanding. The codebases that matter are too complex, too context-dependent, too full of edge cases for prompt-engineering alone.

To the "AI is just autocomplete" crowd: You're underestimating the shift. Developers who refuse to incorporate AI into their workflow will be outpaced by those who do. The productivity difference is real.

The truth is in the middle, as it usually is.

**AI makes good developers more productive. It doesn't make non-developers into developers.**

If you understand systems architecture, AI helps you build faster. If you don't, AI helps you build broken things faster.

## The Actual Threat

Here's what I think will actually happen:

Junior developer roles will shrink. The "write basic CRUD operations all day" jobs are genuinely at risk, because AI can write basic CRUD operations faster than humans.

But senior roles will grow. Someone needs to:
- Design the systems AI implements
- Review the code AI generates
- Debug the failures AI can't explain
- Make the architectural decisions AI can't make
- Understand the business context AI doesn't have

The path from junior to senior traditionally involved years of writing that basic code. If AI writes it instead, how do juniors gain experience? This is a real problem the industry will need to solve.

But the idea that we'll all describe apps in English and AI will build them? That's science fiction. At least for the systems that matter.

## My Advice

**If you're a developer:**
Learn to work with AI. Seriously. It's not going away. But don't stop learning the fundamentals. The fundamentals are what let you direct AI effectively and catch its mistakes.

**If you're learning to code:**
Don't skip the hard parts. Understanding how things work—not just what prompt to type—is what separates developers who can build real systems from people who can make demos.

**If you're a manager:**
AI-assisted developers are more productive, but they still need to be developers. Don't fall for pitches about replacing your team with "AI engineers" who just write prompts.

**If you're posting LinkedIn hot takes:**
Maybe try building something complex with AI first? The nuance might surprise you.

---

*This post was written by a human with AI assistance. The architectural decisions, the arguments, and the opinions are mine. The draft-writing, editing suggestions, and commit messages were a collaboration. That's how it works now. Pretending otherwise is either naivety or marketing.*
