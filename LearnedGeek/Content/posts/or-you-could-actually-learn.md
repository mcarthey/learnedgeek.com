# "Or You Could Actually Learn CSS"

---

## The Comment

I wrote a post comparing Tailwind to Bootstrap. It got some great discussion on LinkedIn — thoughtful questions, practical concerns, people sharing their own experiences switching between frameworks.

And then, right on schedule:

> "Or you could actually learn CSS."

You've seen this comment before. Maybe not about CSS specifically, but the structure is always the same:

- *"ORMs? Or you could actually learn SQL."*
- *"TypeScript? Or you could actually learn JavaScript."*
- *"React? Or you could actually learn the DOM."*
- *"AI tools? Or you could actually learn to code."*

The implication: if you use the tool, you must not understand the thing underneath it. The tool is a crutch. A shortcut for people who skipped the hard part.

I want to push back on this — gently — because the premise is wrong, and I think the people saying it know better.

## The Part That's True

Let's start with what's valid, because there *is* something valid here.

There are developers who can't write a media query because they've only ever used responsive utility classes. There are people who copy-paste from ChatGPT without reading what it generated. There are "full-stack developers" whose entire SQL knowledge is `.Include()` and `.ToListAsync()`.

That's a real concern. Abstractions are dangerous when you don't understand what they're abstracting. If Tailwind is your *first* exposure to CSS concepts and you have no idea what `display: flex` actually does, then yes — you have a gap.

But here's the thing: that's an argument for learning fundamentals *and* using tools. Not one or the other.

## The Part That's Wrong

The false premise is that tool adoption signals ignorance. That choosing Tailwind means you couldn't write CSS if you had to. That using an ORM means you don't know SQL.

In my experience, it's usually the opposite.

I didn't switch to Tailwind because I couldn't write CSS. I switched because I'd written *enough* CSS to know where the pain points are — specificity conflicts, naming conventions that drift over time, the cognitive overhead of context-switching between HTML and stylesheet files. Tailwind solved problems I could only identify *because* I understood the fundamentals.

The same pattern shows up everywhere:

- The developers I know who love TypeScript are the ones who've been burned by JavaScript's type coercion enough times to want guardrails.
- The people who appreciate ORMs are the ones who've hand-written enough SQL to know which queries are worth hand-writing and which aren't.
- The engineers who get the most out of AI coding tools are the ones who can read the output and immediately spot when it's wrong.

**Knowing the fundamentals doesn't mean you have to use them raw forever.** A carpenter who understands wood grain isn't cheating by using a power saw instead of a hand saw. They're being efficient — and they'll notice when the power saw is making a bad cut precisely because they understand the material.

## The Historical Pattern

This reaction isn't new. Every generation of developer tooling gets the same pushback:

**"Or you could actually write assembly."** — said about C in the 1970s.

**"Or you could actually manage your own memory."** — said about garbage-collected languages in the 1990s.

**"Or you could actually write vanilla JavaScript."** — said about jQuery in 2006, then React in 2013.

**"Or you could actually write CSS."** — said about Tailwind in 2024.

The tools that survive aren't the ones that let you skip understanding. They're the ones that let you *apply* understanding more efficiently. C didn't replace the need to think about memory and performance. React didn't replace the need to understand the DOM. Tailwind didn't replace the need to understand CSS layout, spacing, or responsive design — it just gave you a faster vocabulary for expressing that understanding.

## The AI Chapter

This conversation gets more interesting — and more urgent — when AI enters the picture.

"Or you could actually learn to code" is the 2025 version of every comment listed above. And the concern is more understandable here because the abstraction layer is thicker. When someone uses Tailwind, they're still writing code that maps directly to CSS properties. When someone uses an AI tool, the mapping is less transparent.

But the principle holds. The developers getting the most out of AI tools aren't the ones who say "build me an app" and hope for the best. They're the ones who can:

- Read the generated code and understand what it does
- Spot architectural mistakes before they become technical debt
- Direct the tool toward the right solution because they know what "right" looks like
- Know when to throw away the AI's suggestion and write it themselves

I use AI tools in my work. I'm transparent about that — if you've read my reverse engineering series, you'll know I spent a full section talking about what Claude did vs what I did and why the distinction matters. The AI didn't replace my knowledge. It multiplied it. And the multiplication only works because there's something to multiply.

**A developer with no fundamentals plus AI is dangerous.** They'll ship code they can't debug, can't maintain, and can't explain. But a developer with strong fundamentals plus AI? That's someone building things that would have taken a team of five a year ago.

## The Generous Reading

Here's what I think is actually happening when someone drops "or you could actually learn CSS" under a post:

They're not really arguing against tools. They're expressing frustration with a culture that sometimes *does* skip fundamentals. They've worked with junior developers who couldn't debug a CSS issue because they'd never written a line of it. They've reviewed PRs where AI-generated code was clearly not understood by the person submitting it.

That frustration is legitimate. The conclusion — that modern tools are the problem — isn't.

The problem isn't Tailwind. The problem isn't AI. The problem is the same one it's always been: some people take shortcuts without understanding what they're shortcutting. No amount of removing tools fixes that. It just makes the people who *do* understand things slower.

## The Actual Advice

If you're early in your career:

**Learn the fundamentals.** Seriously. Write vanilla CSS until you understand the box model, specificity, flexbox, and grid. Write raw SQL until you can join tables and understand query plans. Build something without a framework so you know what frameworks are doing for you.

**Then use every tool available to you.** Use Tailwind. Use ORMs. Use AI. Use them *because* you understand what's happening underneath, not instead of understanding it.

These things are not mutually exclusive. They never were.
