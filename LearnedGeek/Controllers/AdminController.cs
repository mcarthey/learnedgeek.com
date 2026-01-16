using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LearnedGeek.Models;
using LearnedGeek.Services;

namespace LearnedGeek.Controllers;

[Route("admin")]
public class AdminController : Controller
{
    private readonly AdminSettings _adminSettings;
    private readonly ILinkedInService _linkedInService;
    private readonly IBlogService _blogService;
    private readonly ILogger<AdminController> _logger;
    private const string AdminCookieName = "lg_admin_auth";

    public AdminController(
        IOptions<AdminSettings> adminSettings,
        ILinkedInService linkedInService,
        IBlogService blogService,
        ILogger<AdminController> logger)
    {
        _adminSettings = adminSettings.Value;
        _linkedInService = linkedInService;
        _blogService = blogService;
        _logger = logger;
    }

    private bool IsAuthorized()
    {
        // Check IP whitelist first
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (remoteIp != null && _adminSettings.AllowedIPs.Length > 0)
        {
            // Handle IPv6 localhost mapping
            var normalizedIp = remoteIp == "::1" ? "127.0.0.1" : remoteIp;
            if (_adminSettings.AllowedIPs.Contains(normalizedIp) ||
                _adminSettings.AllowedIPs.Contains(remoteIp))
            {
                return true;
            }
        }

        // Check auth cookie
        if (Request.Cookies.TryGetValue(AdminCookieName, out var cookieValue))
        {
            // Simple hash check - not cryptographically strong but good enough for single-user admin
            var expectedHash = ComputeHash(_adminSettings.Password);
            return cookieValue == expectedHash;
        }

        return false;
    }

    private static string ComputeHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input + "lg_admin_salt_2026");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Set noindex header
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (!IsAuthorized())
        {
            return RedirectToAction(nameof(Login));
        }

        var posts = await _blogService.GetAllPostsAsync();
        ViewBag.LinkedInConfigured = _linkedInService.IsConfigured;
        ViewBag.LinkedInConnected = _linkedInService.HasValidToken;

        return View(posts.OrderByDescending(p => p.Date).ToList());
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (IsAuthorized())
        {
            return RedirectToAction(nameof(Index));
        }

        return View();
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string password)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (string.IsNullOrEmpty(_adminSettings.Password))
        {
            ModelState.AddModelError("", "Admin password not configured.");
            return View();
        }

        if (password == _adminSettings.Password)
        {
            var hash = ComputeHash(password);
            Response.Cookies.Append(AdminCookieName, hash, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            _logger.LogInformation("Admin login successful from {IP}",
                HttpContext.Connection.RemoteIpAddress);

            return RedirectToAction(nameof(Index));
        }

        _logger.LogWarning("Failed admin login attempt from {IP}",
            HttpContext.Connection.RemoteIpAddress);

        ModelState.AddModelError("", "Invalid password.");
        return View();
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AdminCookieName);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("linkedin/connect")]
    public IActionResult LinkedInConnect()
    {
        if (!IsAuthorized())
        {
            return RedirectToAction(nameof(Login));
        }

        if (!_linkedInService.IsConfigured)
        {
            TempData["Error"] = "LinkedIn Client ID and Secret not configured.";
            return RedirectToAction(nameof(Index));
        }

        // Generate state for CSRF protection
        var state = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("linkedin_state", state);

        var authUrl = _linkedInService.GetAuthorizationUrl(state);
        return Redirect(authUrl);
    }

    [HttpGet("linkedin/callback")]
    public async Task<IActionResult> LinkedInCallback(string code, string state, string? error)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (!IsAuthorized())
        {
            return RedirectToAction(nameof(Login));
        }

        if (!string.IsNullOrEmpty(error))
        {
            TempData["Error"] = $"LinkedIn authorization failed: {error}";
            return RedirectToAction(nameof(Index));
        }

        // Verify state
        var savedState = HttpContext.Session.GetString("linkedin_state");
        if (state != savedState)
        {
            TempData["Error"] = "Invalid state parameter. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        // Exchange code for token
        var tokenResponse = await _linkedInService.ExchangeCodeForTokenAsync(code);
        if (tokenResponse == null)
        {
            TempData["Error"] = "Failed to get access token from LinkedIn.";
            return RedirectToAction(nameof(Index));
        }

        // Get member ID
        var memberId = await _linkedInService.GetMemberIdAsync(tokenResponse.AccessToken);
        if (memberId == null)
        {
            TempData["Error"] = "Failed to get LinkedIn member ID.";
            return RedirectToAction(nameof(Index));
        }

        // Store tokens - you'll need to save these to appsettings or a secure store
        TempData["Success"] = $"LinkedIn connected successfully! " +
            $"Please add these to your appsettings.json LinkedIn section:\n" +
            $"AccessToken: {tokenResponse.AccessToken}\n" +
            $"MemberId: {memberId}";

        _logger.LogInformation("LinkedIn OAuth completed. MemberId: {MemberId}", memberId);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("share")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShareToLinkedIn(string slug, string commentary)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (!_linkedInService.HasValidToken)
        {
            return Json(new { success = false, message = "LinkedIn not connected." });
        }

        var post = await _blogService.GetPostBySlugAsync(slug);
        if (post == null)
        {
            return Json(new { success = false, message = "Post not found." });
        }

        var articleUrl = $"https://learnedgeek.com/blog/{slug}";
        var text = string.IsNullOrWhiteSpace(commentary)
            ? $"{post.Title}\n\n{post.Description}"
            : commentary;

        var result = await _linkedInService.SharePostAsync(text, articleUrl);

        if (result.Success)
        {
            _logger.LogInformation("Shared post {Slug} to LinkedIn", slug);
        }

        return Json(new {
            success = result.Success,
            message = result.Success ? "Posted to LinkedIn!" : result.ErrorMessage
        });
    }
}
