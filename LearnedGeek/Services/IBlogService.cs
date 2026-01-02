using LearnedGeek.Models;

namespace LearnedGeek.Services;

public interface IBlogService
{
    Task<IEnumerable<BlogPost>> GetAllPostsAsync();
    Task<IEnumerable<BlogPost>> GetPostsByCategoryAsync(Category category);
    Task<BlogPost?> GetPostBySlugAsync(string slug);
    Task<IEnumerable<BlogPost>> GetFeaturedPostsAsync();
}
