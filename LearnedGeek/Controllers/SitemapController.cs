using Microsoft.AspNetCore.Mvc;
using LearnedGeek.Services;
using System.Text;
using System.Xml.Linq;

namespace LearnedGeek.Controllers;

public class SitemapController : Controller
{
    private readonly IBlogService _blogService;

    public SitemapController(IBlogService blogService)
    {
        _blogService = blogService;
    }

    [Route("sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Index()
    {
        var baseUrl = "https://learnedgeek.com";

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var sitemap = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "urlset",
                CreateUrlElement(ns, $"{baseUrl}/", "weekly", "1.0"),
                CreateUrlElement(ns, $"{baseUrl}/Home/About", "monthly", "0.8"),
                CreateUrlElement(ns, $"{baseUrl}/Home/Work", "monthly", "0.8"),
                CreateUrlElement(ns, $"{baseUrl}/Home/Services", "monthly", "0.8"),
                CreateUrlElement(ns, $"{baseUrl}/Home/Contact", "monthly", "0.6"),
                CreateUrlElement(ns, $"{baseUrl}/Blog", "daily", "0.9")
            )
        );

        var posts = await _blogService.GetAllPostsAsync();
        foreach (var post in posts)
        {
            sitemap.Root!.Add(CreateUrlElement(
                ns,
                $"{baseUrl}/Blog/Post/{post.Slug}",
                "monthly",
                "0.7",
                post.Date
            ));
        }

        return Content(sitemap.ToString(), "application/xml", Encoding.UTF8);
    }

    private static XElement CreateUrlElement(XNamespace ns, string loc, string changefreq, string priority, DateTime? lastmod = null)
    {
        var element = new XElement(ns + "url",
            new XElement(ns + "loc", loc),
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority)
        );

        if (lastmod.HasValue)
        {
            element.Add(new XElement(ns + "lastmod", lastmod.Value.ToString("yyyy-MM-dd")));
        }

        return element;
    }
}
