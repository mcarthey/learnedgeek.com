namespace LearnedGeek.Models;

/// <summary>
/// reCAPTCHA configuration settings
/// </summary>
public class RecaptchaSettings
{
    /// <summary>
    /// Site key (public) - used in the client-side widget
    /// </summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret key (private) - used for server-side validation
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Minimum score (0.0 to 1.0) to consider valid. Default is 0.1
    /// Higher scores indicate more likely human users.
    /// Note: Mobile users often score 0.1-0.3 due to less browsing history,
    /// VPNs, and carrier NAT. Combined with honeypot, 0.1 catches bots (who score 0.0)
    /// while allowing legitimate mobile users.
    /// </summary>
    public float MinimumScore { get; set; } = 0.1f;
}
