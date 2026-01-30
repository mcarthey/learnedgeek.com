namespace LearnedGeek.Models;

public class BlogPost
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Category Category { get; set; }
    public string[] Tags { get; set; } = [];
    public DateTime Date { get; set; }
    public bool Featured { get; set; }
    public string? Image { get; set; }
    public string? LinkedInHook { get; set; }
    public DateTime? LinkedInPostedDate { get; set; }
    public string? Content { get; set; }
    public string? HtmlContent { get; set; }
}
