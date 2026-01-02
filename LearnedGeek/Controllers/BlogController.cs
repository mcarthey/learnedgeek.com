using Microsoft.AspNetCore.Mvc;
using LearnedGeek.Models;
using LearnedGeek.Services;

namespace LearnedGeek.Controllers;

public class BlogController : Controller
{
    private readonly IBlogService _blogService;

    public BlogController(IBlogService blogService)
    {
        _blogService = blogService;
    }

    public async Task<IActionResult> Index(string? category, int? year, int? month)
    {
        var allPosts = await _blogService.GetAllPostsAsync();
        IEnumerable<BlogPost> posts = allPosts;

        // Filter by category if specified
        if (!string.IsNullOrEmpty(category) && Enum.TryParse<Category>(category, true, out var cat))
        {
            posts = posts.Where(p => p.Category == cat);
            ViewBag.SelectedCategory = cat;
        }
        else
        {
            ViewBag.SelectedCategory = null;
        }

        // Filter by year/month if specified
        if (year.HasValue)
        {
            posts = posts.Where(p => p.Date.Year == year.Value);
            ViewBag.SelectedYear = year.Value;

            if (month.HasValue)
            {
                posts = posts.Where(p => p.Date.Month == month.Value);
                ViewBag.SelectedMonth = month.Value;
            }
        }

        // Pass all posts for sidebar archive computation
        ViewBag.AllPosts = allPosts;

        return View(posts);
    }

    [Route("Blog/Post/{slug}")]
    public async Task<IActionResult> Post(string slug)
    {
        var post = await _blogService.GetPostBySlugAsync(slug);

        if (post == null)
            return NotFound();

        // Get related posts from the same category
        var relatedPosts = (await _blogService.GetPostsByCategoryAsync(post.Category))
            .Where(p => p.Slug != post.Slug)
            .Take(3);

        ViewBag.RelatedPosts = relatedPosts;

        return View(post);
    }
}
