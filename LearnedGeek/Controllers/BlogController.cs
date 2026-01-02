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

    public async Task<IActionResult> Index(string? category)
    {
        IEnumerable<BlogPost> posts;

        if (!string.IsNullOrEmpty(category) && Enum.TryParse<Category>(category, true, out var cat))
        {
            posts = await _blogService.GetPostsByCategoryAsync(cat);
            ViewBag.SelectedCategory = cat;
        }
        else
        {
            posts = await _blogService.GetAllPostsAsync();
            ViewBag.SelectedCategory = null;
        }

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
