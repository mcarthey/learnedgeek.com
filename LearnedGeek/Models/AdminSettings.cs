namespace LearnedGeek.Models;

public class AdminSettings
{
    public string Password { get; set; } = string.Empty;
    public string[] AllowedIPs { get; set; } = [];
}
