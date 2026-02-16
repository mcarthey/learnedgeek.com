using System.Text.Json;
using System.Text.Json.Serialization;
using LearnedGeek.Models;
using Markdig;
using Microsoft.Extensions.Logging;

namespace LearnedGeek.Services;

public class BlogService : IBlogService
{
    private readonly string _contentPath;
    private readonly MarkdownPipeline _markdownPipeline;
    private List<BlogPost>? _postsCache;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<BlogService> _logger;

    public BlogService(IWebHostEnvironment env, ILogger<BlogService> logger)
    {
        _contentPath = Path.Combine(env.ContentRootPath, "Content");
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        _logger = logger;
    }

    private async Task<List<BlogPost>> LoadPostsMetadataAsync()
    {
        if (_postsCache != null)
            return _postsCache;

        var postsJsonPath = Path.Combine(_contentPath, "posts.json");
        if (!File.Exists(postsJsonPath))
        {
            _logger.LogWarning("posts.json not found at {Path}", postsJsonPath);
            _postsCache = [];
            return _postsCache;
        }

        try
        {
            var json = await File.ReadAllTextAsync(postsJsonPath);
            var postsData = JsonSerializer.Deserialize<PostsContainer>(json, _jsonOptions);
            _postsCache = postsData?.Posts ?? [];

            _logger.LogInformation("Successfully loaded {Count} blog posts from posts.json", _postsCache.Count);
            return _postsCache;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to deserialize posts.json. This usually means there's an invalid category value or malformed JSON. " +
                "Error at path: {Path}, Line: {LineNumber}, Position: {BytePosition}",
                ex.Path, ex.LineNumber, ex.BytePositionInLine);

            // Return empty list so the site doesn't crash - admin can fix the JSON
            _postsCache = [];
            return _postsCache;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading posts.json from {Path}", postsJsonPath);
            _postsCache = [];
            return _postsCache;
        }
    }

    public async Task<IEnumerable<BlogPost>> GetAllPostsAsync()
    {
        var posts = await LoadPostsMetadataAsync();
        return posts
            .Where(p => p.Date.Date <= DateTime.Today)
            .OrderByDescending(p => p.Date);
    }

    public async Task<IEnumerable<BlogPost>> GetAllPostsIncludingFutureAsync()
    {
        var posts = await LoadPostsMetadataAsync();
        return posts.OrderByDescending(p => p.Date);
    }

    public async Task<IEnumerable<BlogPost>> GetPostsByCategoryAsync(Category category)
    {
        var posts = await LoadPostsMetadataAsync();
        return posts
            .Where(p => p.Category == category && p.Date.Date <= DateTime.Today)
            .OrderByDescending(p => p.Date);
    }

    public async Task<BlogPost?> GetPostBySlugAsync(string slug)
    {
        var posts = await LoadPostsMetadataAsync();
        var post = posts.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (post == null)
            return null;

        // Load and render the markdown content
        var mdPath = Path.Combine(_contentPath, "posts", $"{post.Slug}.md");
        if (File.Exists(mdPath))
        {
            post.Content = await File.ReadAllTextAsync(mdPath);
            post.HtmlContent = Markdown.ToHtml(post.Content, _markdownPipeline);
        }

        return post;
    }

    public async Task<IEnumerable<BlogPost>> GetFeaturedPostsAsync()
    {
        var posts = await LoadPostsMetadataAsync();
        return posts
            .Where(p => p.Featured && p.Date.Date <= DateTime.Today)
            .OrderByDescending(p => p.Date);
    }

    public async Task<Dictionary<string, int>> GetTagCountsAsync()
    {
        var posts = await LoadPostsMetadataAsync();
        return posts
            .Where(p => p.Date.Date <= DateTime.Today)
            .SelectMany(p => p.Tags)
            .GroupBy(t => t.ToLowerInvariant())
            .ToDictionary(g => g.First(), g => g.Count());
    }

    public async Task<IEnumerable<BlogPost>> GetPostsByTagAsync(string tag)
    {
        var posts = await LoadPostsMetadataAsync();
        return posts
            .Where(p => p.Date.Date <= DateTime.Today &&
                        p.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.Date);
    }

    public async Task<bool> UpdatePostLinkedInDateAsync(string slug, DateTime postedDate)
    {
        var posts = await LoadPostsMetadataAsync();
        var post = posts.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (post == null)
            return false;

        post.LinkedInPostedDate = postedDate;

        // Save back to posts.json
        var postsJsonPath = Path.Combine(_contentPath, "posts.json");
        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var container = new PostsContainer { Posts = posts };
        var json = JsonSerializer.Serialize(container, writeOptions);
        await File.WriteAllTextAsync(postsJsonPath, json);

        // Clear cache to ensure fresh data on next load
        _postsCache = null;

        return true;
    }

    public async Task<bool> UpdatePostInstagramDateAsync(string slug, DateTime postedDate)
    {
        var posts = await LoadPostsMetadataAsync();
        var post = posts.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (post == null)
            return false;

        post.InstagramPostedDate = postedDate;

        await SavePostsAsync(posts);
        return true;
    }

    public async Task<bool> UpdatePostDateAsync(string slug, DateTime newDate)
    {
        var posts = await LoadPostsMetadataAsync();
        var post = posts.FirstOrDefault(p =>
            string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));

        if (post == null)
            return false;

        post.Date = newDate;

        await SavePostsAsync(posts);
        return true;
    }

    public async Task<IEnumerable<BlogPost>> GetScheduledPostsAsync()
    {
        var posts = await LoadPostsMetadataAsync();
        return posts
            .Where(p => p.Date.Date > DateTime.Today)
            .OrderBy(p => p.Date);
    }

    public async Task<bool> UpdatePostDatesAsync(Dictionary<string, DateTime> slugDates)
    {
        var posts = await LoadPostsMetadataAsync();

        foreach (var (slug, newDate) in slugDates)
        {
            var post = posts.FirstOrDefault(p =>
                string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (post != null)
                post.Date = newDate;
        }

        await SavePostsAsync(posts);
        return true;
    }

    private async Task SavePostsAsync(List<BlogPost> posts)
    {
        var postsJsonPath = Path.Combine(_contentPath, "posts.json");
        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var container = new PostsContainer { Posts = posts };
        var json = JsonSerializer.Serialize(container, writeOptions);
        await File.WriteAllTextAsync(postsJsonPath, json);

        _postsCache = null;
    }

    private class PostsContainer
    {
        public List<BlogPost> Posts { get; set; } = [];
    }
}
