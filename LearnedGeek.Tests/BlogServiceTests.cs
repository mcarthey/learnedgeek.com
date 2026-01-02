using LearnedGeek.Models;
using LearnedGeek.Services;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace LearnedGeek.Tests;

public class BlogServiceTests
{
    private readonly IBlogService _blogService;

    public BlogServiceTests()
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.ContentRootPath).Returns(AppContext.BaseDirectory);

        _blogService = new BlogService(mockEnv.Object);
    }

    [Fact]
    public async Task GetAllPostsAsync_ReturnsPostsOrderedByDateDescending()
    {
        // Act
        var posts = (await _blogService.GetAllPostsAsync()).ToList();

        // Assert
        Assert.Equal(2, posts.Count);
        Assert.Equal("test-post-computers", posts[0].Slug); // 2026-01-01
        Assert.Equal("test-post-woodworking", posts[1].Slug); // 2025-12-15
    }

    [Fact]
    public async Task GetAllPostsAsync_DeserializesCategoryEnumFromString()
    {
        // Act
        var posts = (await _blogService.GetAllPostsAsync()).ToList();

        // Assert
        Assert.Equal(Category.Computers, posts[0].Category);
        Assert.Equal(Category.Woodworking, posts[1].Category);
    }

    [Fact]
    public async Task GetPostsByCategoryAsync_ReturnsOnlyMatchingCategory()
    {
        // Act
        var posts = (await _blogService.GetPostsByCategoryAsync(Category.Computers)).ToList();

        // Assert
        Assert.Single(posts);
        Assert.Equal("test-post-computers", posts[0].Slug);
    }

    [Fact]
    public async Task GetPostsByCategoryAsync_ReturnsEmptyForNoMatches()
    {
        // Act
        var posts = (await _blogService.GetPostsByCategoryAsync(Category.Writing)).ToList();

        // Assert
        Assert.Empty(posts);
    }

    [Fact]
    public async Task GetPostBySlugAsync_ReturnsPostWithContent()
    {
        // Act
        var post = await _blogService.GetPostBySlugAsync("test-post-computers");

        // Assert
        Assert.NotNull(post);
        Assert.Equal("Test Post About Computers", post.Title);
        Assert.NotNull(post.Content);
        Assert.Contains("test post about computers", post.Content, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(post.HtmlContent);
        Assert.Contains("<h2", post.HtmlContent);
    }

    [Fact]
    public async Task GetPostBySlugAsync_ReturnsNullForNonExistentSlug()
    {
        // Act
        var post = await _blogService.GetPostBySlugAsync("non-existent-post");

        // Assert
        Assert.Null(post);
    }

    [Fact]
    public async Task GetPostBySlugAsync_IsCaseInsensitive()
    {
        // Act
        var post = await _blogService.GetPostBySlugAsync("TEST-POST-COMPUTERS");

        // Assert
        Assert.NotNull(post);
        Assert.Equal("test-post-computers", post.Slug);
    }

    [Fact]
    public async Task GetFeaturedPostsAsync_ReturnsOnlyFeaturedPosts()
    {
        // Act
        var posts = (await _blogService.GetFeaturedPostsAsync()).ToList();

        // Assert
        Assert.Single(posts);
        Assert.True(posts[0].Featured);
        Assert.Equal("test-post-computers", posts[0].Slug);
    }

    [Fact]
    public async Task GetAllPostsAsync_DeserializesAllProperties()
    {
        // Act
        var posts = (await _blogService.GetAllPostsAsync()).ToList();
        var post = posts.First(p => p.Slug == "test-post-computers");

        // Assert
        Assert.Equal("test-post-computers", post.Slug);
        Assert.Equal("Test Post About Computers", post.Title);
        Assert.Equal("A test post in the Computers category.", post.Description);
        Assert.Equal(Category.Computers, post.Category);
        Assert.Equal(new[] { "test", "computers" }, post.Tags);
        Assert.Equal(new DateTime(2026, 1, 1), post.Date);
        Assert.True(post.Featured);
        Assert.Null(post.Image);
    }
}
