namespace LearnedGeek.Models;

public class SeoMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Image { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = "website";
    public DateTime? PublishedTime { get; set; }
    public string? Author { get; set; }
    public string[]? Tags { get; set; }
}
