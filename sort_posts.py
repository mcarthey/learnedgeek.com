#!/usr/bin/env python3
"""Sort posts.json by date (newest first) and add new posts."""

import json
from datetime import datetime

# Read current posts.json
with open('LearnedGeek/Content/posts.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

# New posts to add
new_posts = [
    {
        "slug": "memory-leak-part-1-the-investigation",
        "title": "The Two-Year Memory Leak: How I Found Five Missing `using` Statements",
        "description": "Our high-volume API threw timeouts for two years. External consultants missed it. Infrastructure upgrades didn't fix it. Then I found five missing `using` statements.",
        "category": "Tech",
        "tags": ["csharp", "dotnet", "memory-leak", "debugging", "performance", "production"],
        "date": "2026-09-25",
        "featured": False,
        "image": "/img/posts/memory-leak-part-1.svg",
        "linkedInHook": "Our production API leaked memory for 2 years. External consultants reviewed the code. \"Looks fine,\" they said.\n\nWe threw more hardware at it. The problem persisted.\n\nThen one Friday, my boss mentioned he was \"super excited\" about diagnostics I'd been running. I hadn't told him I'd found the solution yet.\n\nThat weekend, I found all five memory leaks. By Monday, the fix was deployed.\n\nThe investigation:\n- Visual Studio profiler showed 194k objects → 5.5M objects in 30 seconds\n- HttpContext: 285,590 instances (should be < 100)\n- HttpRequestMessage: 2,289 instances\n- HttpResponseMessage: 2,257 instances\n\nEvery leak followed the same pattern: IDisposable objects created but never disposed.\n\nTwo years of production pain. Five `using` statements.\n\n#csharp #dotnet #debugging #performance"
    },
    {
        "slug": "memory-leak-part-2-idisposable-fundamentals",
        "title": "IDisposable: What I Misunderstood for 30 Years",
        "description": "Objects don't dispose themselves when they go out of scope. I believed they did. For three decades. Here's why C# works this way — and why it matters.",
        "category": "Tech",
        "tags": ["csharp", "dotnet", "idisposable", "memory-management", "fundamentals", "best-practices"],
        "date": "2026-09-30",
        "featured": False,
        "image": "/img/posts/memory-leak-part-2.svg",
        "linkedInHook": "After 30 years of C# development, I just learned that objects don't dispose themselves when they go out of scope.\n\nI thought they did. So did most developers I know.\n\nQuick quiz: What happens when this method exits?\n\n```csharp\nvoid ProcessData()\n{\n    var stream = new FileStream(\"data.txt\", FileMode.Open);\n    var reader = new StreamReader(stream);\n    var content = reader.ReadToEnd();\n    // Are stream and reader disposed here?\n}\n```\n\nIf you answered \"yes,\" you're wrong. And you're in good company.\n\nWhat actually happens:\n✅ Local variables go out of scope (stack cleanup)\n✅ References to objects removed\n❌ Objects themselves NOT disposed\n❌ File handles remain open\n⏰ Eventually GC collects them\n🔥 Or you run out of file handles first\n\nC# has three types of cleanup:\n1. Stack cleanup (automatic, immediate)\n2. Heap cleanup (automatic, eventually)\n3. Resource cleanup (manual, explicit)\n\nThe fix: `using` statements. Always.\n\nOne misconception. Two years of production leaks.\n\n#csharp #dotnet #fundamentals #memoryleak"
    },
    {
        "slug": "memory-leak-part-3-web-api-disposal",
        "title": "Web API's Hidden Disposal Trap: HttpResponseMessage vs IHttpActionResult",
        "description": "Returning Task<HttpResponseMessage> from Web API controllers leaks memory. We had 200+ controllers doing it. Here's the fix.",
        "category": "Tech",
        "tags": ["aspnet", "web-api", "memory-leak", "ihttpactionresult", "best-practices", "performance"],
        "date": "2026-10-05",
        "featured": False,
        "image": "/img/posts/memory-leak-part-3.svg",
        "linkedInHook": "Pop quiz: Which Web API controller method leaks memory?\n\nMethod A:\n```csharp\npublic async Task<HttpResponseMessage> GetData()\n{\n    var result = await _service.GetData();\n    return Request.CreateResponse(HttpStatusCode.OK, result);\n}\n```\n\nMethod B:\n```csharp\npublic async Task<IHttpActionResult> GetData()\n{\n    var result = await _service.GetData();\n    return Ok(result);\n}\n```\n\nAnswer: Method A leaks. Badly.\n\nWe had 200+ controllers following this pattern. Every single one leaked HttpResponseMessage objects.\n\nThe problem: Web API's disposal contract differs by return type.\n\n• Return IHttpActionResult → Web API disposes for you ✅\n• Return HttpResponseMessage → You must dispose manually ⚠️\n\nThis isn't documented clearly. Microsoft's own examples show the leaking pattern.\n\nUnder load (100+ req/sec):\n• Before fix: 2,257 HttpResponseMessage instances leaked\n• After fix: < 100 instances\n\nProduction impact:\n• 12 instances → 6 instances (50% cost reduction)\n• Zero timeout errors\n• Stable memory\n\nThe fix: Change return types to IHttpActionResult. Use Ok(), NotFound(), ResponseMessage().\n\nTwo years of pain. One return type change.\n\n#aspnet #webapi #dotnet #performance"
    },
    {
        "slug": "eli5-memory-leaks",
        "title": "ELI5: Why Do Programs Forget to Clean Up After Themselves?",
        "description": "Your computer has a janitor that cleans up trash. But some things need to be thrown away immediately. Here's why programs leak memory — explained with restaurants and dishes.",
        "category": "Tech",
        "tags": ["eli5", "memory-leak", "garbage-collection", "programming", "explainer", "beginners"],
        "date": "2026-10-10",
        "featured": False,
        "image": "/img/posts/eli5-memory-leaks.svg",
        "linkedInHook": "Why do programs leak memory?\n\nImagine running a restaurant kitchen. When customers finish eating, dirty dishes pile up.\n\nOption 1: Stack them and wait for the dishwasher to clean them in bulk\nOption 2: Wash each dish immediately\n\nMost programming languages work like Option 1. They have a \"dishwasher\" called the garbage collector (GC).\n\nThat works great... until you get really busy.\n\nCustomers keep coming. Dishes keep piling up. The dishwasher can't keep up.\n\nEventually, you run out of clean dishes. The kitchen grinds to a halt.\n\nThis is a memory leak.\n\nIn programming, there are two types of trash:\n\n1. Regular trash (memory) - Paper plates. Can wait for the GC.\n2. Special trash (resources) - Dishes with food still on them. Need immediate cleanup.\n\nFile handles, network connections, database connections — these are \"dishes.\" If you don't clean them immediately, you run out.\n\nThe fix: Explicit cleanup. In C#, that's `using` statements.\n\nOur production leak: We forgot to \"wash our dishes\" for 2 years. Five `using` statements fixed it.\n\nThe dishwasher isn't magic. Sometimes you have to wash your own dishes.\n\n#programming #eli5 #memoryleak #explained"
    },
    {
        "slug": "ai-cadence-and-technical-writing",
        "title": "The AI Cadence Problem (And Why I'm Okay With It)",
        "description": "Everyone's hunting for em dashes. Meanwhile, the real tell is the cadence. You'll FEEL it. Here's when I care about AI voice — and when I don't.",
        "category": "Writing",
        "tags": ["ai", "writing", "blogging", "voice", "technical-writing", "meta"],
        "date": "2026-03-22",
        "featured": False,
        "image": "/img/posts/ai-cadence-and-technical-writing.svg",
        "linkedInHook": "People keep asking if I use AI to write my blog.\n\nThe answer: Yes. Obviously.\n\nBut here's the thing everyone misses: The real tell isn't em dashes or oxford commas. It's cadence.\n\nAI writes in perfect, predictable rhythm. Lands neatly. Wraps up cleanly.\n\nHuman voice has rough edges. Surprise. Rhythm that shifts.\n\nWhen does this matter?\n\n❌ Brand voice. Personal narrative. Storytelling.\n✅ Technical documentation. Step-by-step guides. Process posts.\n\nMy blog is my external brain. I write to document what I learn so I don't forget it later.\n\nFor THAT purpose? AI cadence works.\n\nBut if I were writing thought leadership or building a personal brand? I'd need to break the rhythm. Add friction. Let my actual voice come through.\n\nThe trick isn't hiding AI. It's knowing when perfect cadence helps — and when it kills you.\n\nBlog post: https://learnedgeek.com/Blog/Post/ai-cadence-and-technical-writing\n\n#ai #writing #blogging #voice"
    }
]

# Add new posts
added_count = 0
for post in new_posts:
    # Check if already exists
    if not any(p['slug'] == post['slug'] for p in data['posts']):
        data['posts'].append(post)
        added_count += 1

# Sort by date (newest first)
data['posts'].sort(key=lambda x: datetime.strptime(x['date'], '%Y-%m-%d'), reverse=True)

# Write back
with open('LearnedGeek/Content/posts.json', 'w', encoding='utf-8') as f:
    json.dump(data, f, indent=2, ensure_ascii=False)

print(f"Added {added_count} new posts and sorted {len(data['posts'])} total posts by date")
