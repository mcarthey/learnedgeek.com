I've been using ChatGPT almost daily for two years. Over a thousand conversations about debugging code, brainstorming ideas, learning new concepts, planning projects, even figuring out what to make for dinner.

Then I realized: I have no idea what's actually in there.

So I exported everything, wrote some code to analyze it, and discovered things about my own thinking patterns I never expected. But before I tell you what I found, I need to explain *how* I found it—because the techniques involved sound intimidating but are actually kind of magical once you see how they work.

## The Problem: 200 Megabytes of Chaos

OpenAI lets you export your conversation history. Settings → Data Controls → Export Data. I did this expecting to skim through some memorable chats.

What I got was a 200 megabyte JSON file that crashed my text editor.

This is the first problem: **your AI conversations are trapped**. You can't search them meaningfully. You can't see patterns. You can't answer "have I asked about this before?" or "what was that solution I found last month?"

I decided to fix that. But first, I needed to understand some concepts that sounded scary but turned out to be beautifully simple.

## Concept 1: Embeddings (GPS for Words)

Here's the key insight that makes everything else possible: **computers can turn words into locations on a map**.

Not a physical map—a *meaning* map. A map with hundreds of dimensions instead of two. But the principle is the same as GPS coordinates.

Think about it this way:

- The word "dog" might live at coordinates (45.2, 78.1, 12.5, ...)
- The word "puppy" lives nearby at (45.1, 77.9, 12.8, ...)
- The word "database" lives far away at (12.4, 3.2, 89.1, ...)

"Dog" and "puppy" are close together because they *mean* similar things. "Dog" and "database" are far apart because they don't.

This is what an "embedding" is: GPS coordinates for meaning.

The computer doesn't *understand* language the way you do. It just learned—by reading billions of documents—where different words and phrases tend to live on this map. Synonyms cluster together. Related concepts cluster together. Unrelated things end up far apart.

When I convert a conversation about "debugging Python code" into an embedding, I get coordinates like (23.1, 67.4, 91.2, ...). When I convert a conversation about "planning a birthday party," I get completely different coordinates like (78.3, 12.1, 45.8, ...).

**The magic:** I can now ask mathematical questions like "which of my 1,290 conversations are *closest* to each other on the meaning map?"

## Concept 2: Clustering (The Sock Drawer Approach)

Imagine dumping 1,000 photos on a table and sorting them into piles.

You don't read metadata. You don't check timestamps. You just *look* at them and go: "These are beach photos. Those are work events. These are pictures of my dog. Those are screenshots of code."

You're finding natural groupings based on similarity—without being told what categories to use.

That's clustering.

The algorithm I used (called KMeans, but the name doesn't matter) does exactly this with my conversation coordinates:

1. Pick some random starting points on the meaning map
2. Assign each conversation to its nearest point
3. Move the points to the center of their assigned conversations
4. Repeat until the groups stabilize

What falls out are natural groupings: a cluster of conversations about React debugging, another about cooking, one about home automation, one about that novel I'm writing.

**The magic:** Nobody told the algorithm what categories exist. It found them by noticing which conversations live near each other on the meaning map.

## My First Attempt (And Why It Failed)

I converted all 1,290 conversations into embeddings, ran clustering, and got... garbage.

The biggest cluster contained 800+ conversations and was labeled "General Discussion." Useless.

When I investigated, I found the culprit: ChatGPT adds hidden system messages to every conversation containing your custom instructions and memory. Every single chat started with the same 500-word preamble.

The algorithm saw this identical text and concluded: "These are all about the same thing!"

I filtered out system messages and tried again. Better, but still messy.

## The Real Problem: Conversations Wander

Here's something obvious in retrospect: **my conversations don't stay on topic**.

A single chat might:
1. Start with a TypeScript error
2. Shift to discussing API design patterns
3. Veer into whether I should use a different framework
4. End with me asking for a recipe because I got hungry while debugging

That's one "conversation" in ChatGPT, but it's really *four different discussions*. When I try to plot this on the meaning map, where does it go? It's about TypeScript AND design patterns AND frameworks AND cooking. It doesn't belong anywhere.

I needed to stop thinking about conversations and start thinking about **segments**.

## Concept 3: Topic Segmentation (The Vibe Shift Detector)

You know that moment at a party when the conversation suddenly changes? You're talking about work gossip, and then someone mentions their weekend plans, and suddenly everyone's discussing travel.

You don't need keywords to notice this. You just *feel* the vibe shift.

I built a mathematical vibe shift detector.

Here's how it works:

1. **Slide a window** across the conversation—look at 4 messages at a time
2. **Convert each window** into embedding coordinates (where is this chunk on the meaning map?)
3. **Compare consecutive windows**—are they pointing in the same direction?
4. **When they diverge sharply**, mark it as a topic boundary

The "pointing in the same direction" part is called cosine similarity. Think of it like this:

- Two flashlights pointing the same way = 1.0 (same topic)
- Two flashlights perpendicular = 0.0 (unrelated topics)
- Two flashlights opposite = -1.0 (opposite meanings)

When the similarity between consecutive windows drops below about 0.55, there's been a vibe shift. New topic. Split here.

After running this segmentation, my 1,290 conversations became roughly **6,000 segments**. Each one focused on a single coherent topic.

## Now Clustering Actually Works

With segments instead of whole conversations, the clusters became meaningful:

**The Greatest Hits**: Some topics appeared across dozens of conversations. I've apparently asked about "structuring React components" at least fifteen times over two years. Not the same question exactly, but variations on the same underlying confusion.

This was humbling. I thought I understood React components. My chat history says otherwise.

**The Learning Arcs**: I could see myself progressing through topics over time. Early conversations about a technology were basic "how does this work?" questions. Later ones were nuanced implementation details. The segments formed a timeline of understanding.

**The Forgotten Solutions**: Multiple times I'd solved a problem, forgotten about it, and solved it again months later. Sometimes identically. Sometimes differently. Having these segments clustered together was like finding notes from past-me.

**The Rabbit Holes**: Some clusters revealed interests I hadn't consciously noticed. A surprising number of segments about woodworking. A consistent curiosity about database concurrency. Patterns I wouldn't have seen without the bird's-eye view.

## The "Have I Asked This Before?" Problem

The most practical outcome: **similarity search**.

Before starting a new conversation, I can now check: "Have I discussed this before?" If so, I see what I learned, what solutions I tried, whether past-me had insights current-me has forgotten.

Here's the beautiful part: it searches by *meaning*, not keywords.

Searching for "database optimization" surfaces:
- Segments that literally contain those words
- Discussions about query performance (same meaning, different words)
- That time I spent an hour on a slow PostgreSQL query (related concept)
- Indexing strategies (same neighborhood on the meaning map)

It's like having a searchable external memory that understands context.

## The Technical Bit (For Those Who Want It)

If you want to build something similar:

**Storage**: Import your export into a real database. I used SQL Server, but SQLite works fine for personal use.

**Embeddings**: I used `nomic-embed-text` running locally via Ollama. Free, fast, and your data never leaves your machine. Each piece of text becomes a 768-dimensional vector (768 GPS coordinates on the meaning map).

**Segmentation**: Sliding window of 4 messages. Compare consecutive window embeddings using cosine similarity. Split when similarity drops below ~0.55.

**Clustering**: KMeans on segment embeddings. The tricky part is choosing how many clusters—I used a heuristic based on segment count.

**Naming**: Another local LLM (Mistral 7B) generates human-readable names from cluster samples. "Cluster 47" becomes "React State Management."

Everything runs locally. Your conversations stay private.

## What I Actually Learned (About Myself)

Beyond the technical project, this taught me things about how I interact with AI:

**I repeat myself more than I thought.** Seeing the same questions clustered together was a wake-up call. Some repetition is fine—revisiting ideas from new angles. But some was just forgetting.

**Conversations are terrible containers for knowledge.** The chat format encourages wandering. That's fine for exploration, but insights get buried. The segment-based view matches how the knowledge actually organizes itself.

**My interests are more consistent than they feel.** Day-to-day, I seem to bounce between random topics. But the clusters show stable themes underneath the chaos. I keep returning to the same questions because they actually matter to me.

**AI conversations are worth preserving.** I used to think of chats as disposable. But they form a detailed record of my thinking over time. That's worth organizing.

## The Bigger Picture

What I built is essentially **a second brain for my AI conversations**.

The concepts involved—embeddings, clustering, similarity search—sound intimidating. But they're really just:

- **Embeddings**: GPS coordinates for meaning
- **Clustering**: Sorting photos into piles by similarity
- **Cosine similarity**: Are these two things pointing the same direction?
- **Segmentation**: Detecting when the vibe shifts

These same techniques power recommendation systems ("you might also like..."), search engines, spam filters, and the AI assistants themselves. Understanding them, even at a high level, helps you understand how modern AI actually works.

And sometimes, it helps you understand yourself.

---

*The code for this project is available on [GitHub](https://github.com/mcarthey/ChatLake). It's a .NET application, but the concepts translate to any language with embedding model access.*

*Next time you're about to ask ChatGPT something, maybe check if past-you already found the answer.*
