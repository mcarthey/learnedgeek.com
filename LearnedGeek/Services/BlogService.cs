using System.Text.Json;
using System.Text.Json.Serialization;
using LearnedGeek.Models;
using Markdig;

namespace LearnedGeek.Services;

public class BlogService : IBlogService
{
    private readonly string _contentPath;
    private readonly MarkdownPipeline _markdownPipeline;
    private List<BlogPost>? _postsCache;
    private readonly JsonSerializerOptions _jsonOptions;

    public BlogService(IWebHostEnvironment env)
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
    }

    private async Task<List<BlogPost>> LoadPostsMetadataAsync()
    {
        if (_postsCache != null)
            return _postsCache;

        var postsJsonPath = Path.Combine(_contentPath, "posts.json");
        if (!File.Exists(postsJsonPath))
        {
            _postsCache = [];
            return _postsCache;
        }

        var json = await File.ReadAllTextAsync(postsJsonPath);
        var postsData = JsonSerializer.Deserialize<PostsContainer>(json, _jsonOptions);
        _postsCache = postsData?.Posts ?? [];

        return _postsCache;
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

    private class PostsContainer
    {
        public List<BlogPost> Posts { get; set; } = [];
    }
}
