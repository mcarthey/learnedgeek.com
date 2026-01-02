namespace LearnedGeek.Models;

public enum Category
{
    Computers,
    Woodworking,
    Writing,
    Automotive,
    HomeImprovement,
    General
}

public static class CategoryExtensions
{
    public static string ToDisplayName(this Category category) => category switch
    {
        Category.Computers => "Computers",
        Category.Woodworking => "Woodworking",
        Category.Writing => "Writing",
        Category.Automotive => "Automotive",
        Category.HomeImprovement => "Home Improvement",
        Category.General => "General",
        _ => category.ToString()
    };

    public static string ToColorClass(this Category category) => category switch
    {
        Category.Computers => "bg-blue-100 text-blue-800",
        Category.Woodworking => "bg-amber-100 text-amber-800",
        Category.Writing => "bg-orange-100 text-orange-800",
        Category.Automotive => "bg-red-100 text-red-800",
        Category.HomeImprovement => "bg-green-100 text-green-800",
        Category.General => "bg-neutral-100 text-neutral-800",
        _ => "bg-neutral-100 text-neutral-800"
    };

    public static string ToSlug(this Category category) => category.ToString().ToLowerInvariant();
}
