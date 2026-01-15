# RSS Feeds and Social Media Automation: Publishing Once, Sharing Everywhere

*You wrote a blog post. Now you need to share it on LinkedIn, Twitter, maybe a newsletter. Here's how to automate the boring part.*

**Tags:** rss, automation, linkedin, social-media, zapier, aspnet-core

---

I just set up RSS feeds for this blog. The goal: write a post once, have it automatically shared to LinkedIn and wherever else makes sense.

This post documents the setup while it's fresh. I'll update it as I discover what works and what doesn't.

## Why RSS in 2026?

RSS feels like ancient technology—and it kind of is. But it's also the universal adapter for content automation. Every major automation platform (Zapier, Make, IFTTT) can watch an RSS feed and trigger actions when new items appear.

The alternative is building custom integrations for every platform. RSS lets you build once and connect everywhere.

## The Feed Endpoints

I added two feed formats to this blog:

**RSS 2.0** — The classic format, works everywhere:
```
https://learnedgeek.com/feed.xml
```

**JSON Feed** — A modern alternative that's easier to parse programmatically:
```
https://learnedgeek.com/feed.json
```

Both contain the same data: the 20 most recent posts with title, description, publication date, and tags.

## Building the RSS Controller

The implementation follows the same pattern as the sitemap—a controller that generates XML on the fly:

```csharp
[Route("feed.xml")]
[ResponseCache(Duration = 3600)]
public async Task<IActionResult> Rss()
{
    var posts = await _blogService.GetAllPostsAsync();
    var recentPosts = posts
        .OrderByDescending(p => p.Date)
        .Take(20)
        .ToList();

    XNamespace atom = "http://www.w3.org/2005/Atom";
    var rss = new XDocument(
        new XDeclaration("1.0", "UTF-8", null),
        new XElement("rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "atom", atom),
            new XElement("channel",
                new XElement("title", "Learned Geek"),
                new XElement("link", baseUrl),
                new XElement("description", "..."),
                // ... channel metadata ...
                recentPosts.Select(post => new XElement("item",
                    new XElement("title", post.Title),
                    new XElement("link", $"{baseUrl}/Blog/Post/{post.Slug}"),
                    new XElement("pubDate", post.Date.ToString("R")),
                    new XElement("description", post.Description)
                ))
            )
        )
    );

    return Content(rss.ToString(), "application/rss+xml", Encoding.UTF8);
}
```

Key details:
- **Atom namespace** — Required for the self-referencing link that feed validators expect
- **RFC 822 date format** — The `"R"` format specifier gives you the right format for RSS
- **Response caching** — No need to regenerate on every request; 1 hour is plenty

## Feed Auto-Discovery

For feed readers to automatically find your feed, add these links to your `<head>`:

```html
<link rel="alternate" type="application/rss+xml"
      title="Learned Geek RSS Feed" href="/feed.xml" />
<link rel="alternate" type="application/json"
      title="Learned Geek JSON Feed" href="/feed.json" />
```

This lets browsers and feed readers show the little RSS icon and offer to subscribe.

## Automation Options

With feeds in place, here's how to connect them to social platforms:

### Zapier (Easiest)

1. Create a Zap with trigger "New Item in RSS Feed"
2. Enter your feed URL: `https://yoursite.com/feed.xml`
3. Add action "Create Share Update" for LinkedIn
4. Map title, description, and link to the post fields
5. Test and enable

**Limitations:**
- Free tier: 100 tasks/month
- LinkedIn posts are basic text + link (no rich cards)
- Can only post to personal profiles, not company pages

### IFTTT

Similar flow, sometimes better LinkedIn integration for personal accounts. Worth trying if Zapier's LinkedIn connection doesn't work for you.

### Make.com (formerly Integromat)

More powerful than Zapier, steeper learning curve. Better for complex workflows like "post to LinkedIn AND Twitter AND send a newsletter."

### Buffer / Hootsuite

These aren't RSS-triggered, but you can use Zapier to push RSS items into their queues for scheduled posting.

## The LinkedIn API Route

If you want more control (custom formatting, company page posting, analytics), you'll need to go direct to LinkedIn's API.

**What you need:**
1. LinkedIn Developer account
2. Create an app at developers.linkedin.com
3. Request `w_member_social` permission
4. Implement OAuth 2.0 flow
5. Use the `ugcPosts` endpoint to share content

**The catch:** LinkedIn restricts API access heavily. Getting approved for posting permissions requires explaining your use case and may take weeks.

I'm exploring this route because I'm stubborn. Will update this post with results.

## What I'm Setting Up

My current plan:
1. ✅ RSS feed endpoints deployed
2. ⏳ Zapier free tier for initial LinkedIn automation
3. ⏳ LinkedIn API exploration (for fun/learning)
4. ⏳ Possibly Twitter/X if I ever use it again

## The Feed Validation Checklist

Before connecting automation, validate your feeds:
- [W3C Feed Validation Service](https://validator.w3.org/feed/) for RSS
- [JSON Feed Validator](https://validator.jsonfeed.org/) for JSON Feed

Common issues:
- **Invalid date format** — RSS wants RFC 822 (`Tue, 21 Jan 2026 00:00:00 GMT`)
- **Missing self-link** — Feed validators complain without the Atom self-reference
- **Encoding issues** — Make sure you're returning UTF-8

## Results So Far

*[This section will be updated as I test the automation]*

**LinkedIn via Zapier:** TBD

**LinkedIn API direct:** TBD

**Unexpected issues:** TBD

## The Takeaway

RSS is boring infrastructure, but it's the foundation for "write once, publish everywhere." Setting it up takes an hour; the automation saves time on every future post.

The real test is whether LinkedIn automation actually drives traffic back to the blog. If it does, great. If not, at least I have feeds that work with any future platform I want to try.

---

*This is a living document. I'll update it as I get the automation working (or discover why it doesn't).*
