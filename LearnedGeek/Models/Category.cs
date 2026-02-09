namespace LearnedGeek.Models;

public enum Category
{
    Tech,
    Writing,
    Gaming,
    Project,
    Personal
}

public static class CategoryExtensions
{
    public static string ToDisplayName(this Category category) => category switch
    {
        Category.Tech => "Tech",
        Category.Writing => "Writing",
        Category.Gaming => "Gaming",
        Category.Project => "Project",
        Category.Personal => "Personal",
        _ => category.ToString()
    };

    public static string ToColorClass(this Category category) => category switch
    {
        Category.Tech => "bg-blue-100 text-blue-800",
        Category.Writing => "bg-orange-100 text-orange-800",
        Category.Gaming => "bg-purple-100 text-purple-800",
        Category.Project => "bg-green-100 text-green-800",
        Category.Personal => "bg-amber-100 text-amber-800",
        _ => "bg-neutral-100 text-neutral-800"
    };

    public static string ToSlug(this Category category) => category.ToString().ToLowerInvariant();
}
