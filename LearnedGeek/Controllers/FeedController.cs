using Microsoft.AspNetCore.Mvc;
using LearnedGeek.Services;
using System.Text;
using System.Xml.Linq;

namespace LearnedGeek.Controllers;

public class FeedController : Controller
{
    private readonly IBlogService _blogService;

    public FeedController(IBlogService blogService)
    {
        _blogService = blogService;
    }

    [Route("feed.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Rss()
    {
        var baseUrl = "https://learnedgeek.com";
        var posts = await _blogService.GetAllPostsAsync();

        // Get the 20 most recent posts
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
                    new XElement("description", "Software development, writing, and the occasional side quest into whatever catches my interest."),
                    new XElement("language", "en-us"),
                    new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")),
                    new XElement(atom + "link",
                        new XAttribute("href", $"{baseUrl}/feed.xml"),
                        new XAttribute("rel", "self"),
                        new XAttribute("type", "application/rss+xml")
                    ),
                    recentPosts.Select(post => new XElement("item",
                        new XElement("title", post.Title),
                        new XElement("link", $"{baseUrl}/Blog/Post/{post.Slug}"),
                        new XElement("guid",
                            new XAttribute("isPermaLink", "true"),
                            $"{baseUrl}/Blog/Post/{post.Slug}"
                        ),
                        new XElement("pubDate", post.Date.ToString("R")),
                        new XElement("description", post.Description),
                        post.Tags.Select(tag => new XElement("category", tag))
                    ))
                )
            )
        );

        return Content(rss.ToString(), "application/rss+xml", Encoding.UTF8);
    }

    [Route("feed.json")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Json()
    {
        var baseUrl = "https://learnedgeek.com";
        var posts = await _blogService.GetAllPostsAsync();

        var recentPosts = posts
            .OrderByDescending(p => p.Date)
            .Take(20)
            .ToList();

        var feed = new
        {
            version = "https://jsonfeed.org/version/1.1",
            title = "Learned Geek",
            home_page_url = baseUrl,
            feed_url = $"{baseUrl}/feed.json",
            description = "Software development, writing, and the occasional side quest into whatever catches my interest.",
            language = "en-US",
            items = recentPosts.Select(post => new
            {
                id = $"{baseUrl}/Blog/Post/{post.Slug}",
                url = $"{baseUrl}/Blog/Post/{post.Slug}",
                title = post.Title,
                summary = post.Description,
                date_published = post.Date.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                tags = post.Tags
            })
        };

        return Json(feed);
    }
}
