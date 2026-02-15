# ELI5: Why Do Programs Forget to Clean Up After Themselves?

---

**Summary:** Your computer has a janitor that comes around to clean up trash. But some things need to be thrown away immediately, not left for the janitor. Here's why programs leak memory — explained with restaurants and dishes.

---

## The Restaurant Analogy

Imagine you're running a restaurant kitchen. When a customer finishes eating, what happens to the dirty dishes?

**Option 1: Stack them and wait**
Put the dirty dishes in a pile. Eventually, when the dishwasher has time, they'll come clean them all at once.

**Option 2: Clean them immediately**
Wash each dish as soon as it's done being used.

Most programming languages work like Option 1. They have a "dishwasher" called the **garbage collector** (GC) that comes around and cleans up when it has time.

That works great... until you run out of dishes.

## When Waiting Doesn't Work

Now imagine your restaurant gets really busy. Customers keep coming in. Dishes keep piling up. The dishwasher can't keep up.

Eventually, you run out of clean dishes. The kitchen grinds to a halt. Customers leave. You lose money.

**This is a memory leak.**

The program keeps creating new "dishes" (objects) faster than the "dishwasher" (garbage collector) can clean them up.

## The Two Types of Trash

In programming, there are two types of things that need cleaning up:

### 1. Regular Trash (Memory)

Like paper plates — cheap, disposable, no big deal if they sit around for a bit. The garbage collector handles these.

```
Customer orders food → Creates objects
Customer leaves → Objects become trash
Garbage collector comes by → Objects cleaned up
```

### 2. Special Trash (Resources)

Like dishes with food still on them — they're taking up space *right now*. If you don't wash them immediately, you run out of dishes.

Programming "dishes":
- File handles (you can only have so many files open at once)
- Network connections (your internet can only handle so many at once)
- Database connections (expensive to keep open)

These need to be cleaned up **immediately**, not when the garbage collector feels like it.

## The Problem: Programmers Forget

Most programmers (including me, for 30 years!) think the garbage collector handles everything.

```csharp
void ServeCustomer()
{
    var dish = GetCleanDish();
    ServeFoodOn(dish);
    // Customer is done eating...
    // I thought the dish got washed automatically here. WRONG.
}
```

The dish (object) doesn't get washed (disposed) automatically. It just sits there. Under the program runs out of dishes.

## The Fix: Wash Your Dishes

In programming, you explicitly say "clean this up NOW" using a `using` statement:

```csharp
void ServeCustomer()
{
    using (var dish = GetCleanDish())
    {
        ServeFoodOn(dish);

    } // ✅ Dish is washed HERE, guaranteed
}
```

Think of `using` as a sign on the dish that says "WASH ME IMMEDIATELY WHEN DONE."

## Why Don't All Languages Work This Way?

Some languages (like C++) **do** wash dishes automatically. The moment you're done with something, it's cleaned up. Immediately. Always.

C# (and Java, Python, JavaScript) work differently. They use a garbage collector because:

1. **It's faster** (usually) - washing dishes in bulk is more efficient
2. **It's simpler** (usually) - you don't have to think about cleanup
3. **It handles complex situations** - what if two customers share a dish?

The trade-off: **you** have to remember which things need immediate cleanup (dishes) vs which things can wait for the garbage collector (paper plates).

## The Real-World Story

Our production website was like a restaurant that forgot to wash dishes. Every customer request "borrowed" some dishes. We thought they were getting washed automatically.

They weren't.

After 2-3 minutes of heavy traffic, we'd run out of "dishes" (network connections). The website would crash with timeout errors.

**The fix:** Five lines of code adding `using` statements — the programming equivalent of "WASH THIS DISH NOW."

**The result:**
- Website went from crashing under load to stable
- Cut our server costs in half
- Customers stopped getting timeout errors

All because we finally washed our dishes.

## How to Spot a Memory Leak

**At a restaurant:**
- Kitchen runs out of dishes
- Orders back up
- Service slows down
- Eventually you have to close

**In a program:**
- Memory usage keeps climbing
- Program gets slower and slower
- Eventually it crashes with "out of memory"
- Or the server runs out of connections/file handles first

**The tools:**

Just like a restaurant manager counts dishes, programmers use tools to count objects:

```
Objects at start: 194,071
Objects after 30 seconds: 5,539,099

Houston, we have a problem.
```

## What You Should Remember

1. **Garbage collectors are dishwashers** - They clean up when they have time
2. **Some things can't wait** - File handles, connections, and other "dishes" need immediate cleanup
3. **`using` statements = "wash this now"** - Explicit cleanup
4. **Memory leaks = running out of dishes** - Creating faster than cleaning
5. **Load testing finds leaks** - Light traffic hides the problem; heavy traffic exposes it

## Why This Matters

Every app you use — websites, mobile apps, games — has to manage resources. When they do it wrong, you get:

- Websites that slow down over time
- Apps that crash after being open too long
- Games that lag during intense moments
- Services that timeout during peak hours

Next time an app tells you to "restart for performance," it might be because the developers forgot to wash their dishes.

---

*Want the technical deep dive? Read the full [three-part memory leak debugging series](/Blog/Post/memory-leak-part-1-the-investigation) where I found five missing `using` statements that cost us two years of production pain.*

*The dishwasher isn't magic. Sometimes you have to wash your own dishes.*

---

**Tags:** eli5, memory-leak, garbage-collection, programming, explainer, beginners
