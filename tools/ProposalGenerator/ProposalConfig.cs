namespace ProposalGenerator;

/// <summary>
/// Maps to YAML front matter in proposal markdown files.
/// </summary>
public class ProposalConfig
{
    public string Title { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientCompany { get; set; } = "";
    public string ClientLogo { get; set; } = "";
    public string LgLogo { get; set; } = "learned-geek-logo.png";
    public string Date { get; set; } = "";
    public string Accent { get; set; } = "#4a90d9";
    public string AccentLight { get; set; } = "#e8f0fb";
    public string ClosingQuote { get; set; } = "";
}
