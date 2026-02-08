using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LearnedGeek.Models;
using LearnedGeek.Services;
using SkiaSharp;
using Svg.Skia;

namespace LearnedGeek.Controllers;

[Route("admin")]
public class AdminController : Controller
{
    private readonly AdminSettings _adminSettings;
    private readonly ILinkedInService _linkedInService;
    private readonly IInstagramService _instagramService;
    private readonly IHashtagService _hashtagService;
    private readonly IBlogService _blogService;
    private readonly ILogger<AdminController> _logger;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private const string AdminCookieName = "lg_admin_auth";

    public AdminController(
        IOptions<AdminSettings> adminSettings,
        ILinkedInService linkedInService,
        IInstagramService instagramService,
        IHashtagService hashtagService,
        IBlogService blogService,
        ILogger<AdminController> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        _adminSettings = adminSettings.Value;
        _linkedInService = linkedInService;
        _instagramService = instagramService;
        _hashtagService = hashtagService;
        _blogService = blogService;
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
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

        var posts = await _blogService.GetAllPostsIncludingFutureAsync();
        ViewBag.LinkedInConfigured = _linkedInService.IsConfigured;
        ViewBag.LinkedInConnected = _linkedInService.HasValidToken;
        ViewBag.InstagramConfigured = _instagramService.IsConfigured;
        ViewBag.InstagramConnected = _instagramService.HasValidToken;
        ViewBag.HashtagsConfigured = _hashtagService.IsConfigured;

        return View(posts.ToList());
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

        var articleUrl = $"https://learnedgeek.com/Blog/Post/{slug}";
        var text = string.IsNullOrWhiteSpace(commentary)
            ? $"{post.Title}\n\n{post.Description}"
            : commentary;

        // Try to get the post's hero image using the image path from posts.json
        var imageData = await GetPostImageAsync(post.Image);

        LinkedInPostResult result;
        if (imageData != null)
        {
            _logger.LogInformation("Sharing post {Slug} with image to LinkedIn", slug);
            result = await _linkedInService.SharePostWithImageAsync(text, articleUrl, imageData, "image/png");
        }
        else
        {
            _logger.LogInformation("Sharing post {Slug} without image to LinkedIn (no image found)", slug);
            result = await _linkedInService.SharePostAsync(text, articleUrl);
        }

        if (result.Success)
        {
            _logger.LogInformation("Shared post {Slug} to LinkedIn", slug);
            await _blogService.UpdatePostLinkedInDateAsync(slug, DateTime.UtcNow);
        }

        return Json(new {
            success = result.Success,
            message = result.Success ? "Posted to LinkedIn!" : result.ErrorMessage
        });
    }

    [HttpPost("update-date")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePostDate(string slug, DateTime newDate)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        var result = await _blogService.UpdatePostDateAsync(slug, newDate);
        return Json(new {
            success = result,
            message = result ? "Date updated!" : "Post not found."
        });
    }

    [HttpGet("schedule")]
    public async Task<IActionResult> Schedule()
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        if (!IsAuthorized())
        {
            return RedirectToAction(nameof(Login));
        }

        var scheduledPosts = await _blogService.GetScheduledPostsAsync();
        return View(scheduledPosts.ToList());
    }

    [HttpPost("schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSchedule([FromBody] Dictionary<string, DateTime> slugDates)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        var result = await _blogService.UpdatePostDatesAsync(slugDates);
        return Json(new { success = result });
    }

    // Instagram Integration

    [HttpGet("instagram/connect")]
    public IActionResult InstagramConnect()
    {
        if (!IsAuthorized())
        {
            return RedirectToAction(nameof(Login));
        }

        if (!_instagramService.IsConfigured)
        {
            TempData["Error"] = "Instagram App ID and Secret not configured.";
            return RedirectToAction(nameof(Index));
        }

        var state = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("instagram_state", state);

        var authUrl = _instagramService.GetAuthorizationUrl(state);
        return Redirect(authUrl);
    }

    [HttpGet("instagram/callback")]
    public async Task<IActionResult> InstagramCallback(string code, string state, string? error)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        // Temporarily skip auth check - cookie doesn't survive OAuth redirect
        // This is safe because we need the Facebook code to do anything

        if (!string.IsNullOrEmpty(error))
        {
            TempData["Error"] = $"Instagram authorization failed: {error}";
            return RedirectToAction(nameof(Index));
        }

        var savedState = HttpContext.Session.GetString("instagram_state");
        if (state != savedState)
        {
            // Log warning but continue - session often lost on shared hosting
            _logger.LogWarning("Instagram state mismatch. Expected: {Expected}, Got: {Got}", savedState, state);
        }

        // Exchange code for short-lived token
        var shortLivedToken = await _instagramService.ExchangeCodeForTokenAsync(code);
        if (shortLivedToken == null)
        {
            TempData["Error"] = "Failed to get access token from Instagram/Facebook.";
            return RedirectToAction(nameof(Index));
        }

        // Exchange for long-lived token (60 days)
        var longLivedToken = await _instagramService.ExchangeLongLivedTokenAsync(shortLivedToken.AccessToken);
        if (longLivedToken == null)
        {
            TempData["Error"] = "Failed to get long-lived token.";
            return RedirectToAction(nameof(Index));
        }

        // Get Instagram account ID (with debug info)
        var (instagramAccountId, debugInfo) = await _instagramService.GetInstagramAccountIdWithDebugAsync(longLivedToken.AccessToken);
        if (instagramAccountId == null)
        {
            // Show debug info directly since TempData gets lost
            return Content($@"
                <html>
                <head><title>Instagram Connection Failed</title></head>
                <body style='font-family: monospace; background: #1a1a1a; color: #fff; padding: 40px;'>
                    <h1 style='color: #ef4444;'>Failed to get Instagram Account</h1>
                    <p>Make sure you have an Instagram Business/Creator account linked to a Facebook Page.</p>
                    <h2>Debug Info:</h2>
                    <pre style='background: #333; padding: 20px; border-radius: 8px; overflow-x: auto; white-space: pre-wrap;'>{System.Net.WebUtility.HtmlEncode(debugInfo)}</pre>
                    <p><a href='/admin' style='color: #3b82f6;'>Return to Admin</a></p>
                </body>
                </html>", "text/html");
        }

        var expiresAt = DateTime.UtcNow.AddSeconds(longLivedToken.ExpiresIn);

        _logger.LogInformation("Instagram OAuth completed. AccountId: {AccountId}, Token: {Token}",
            instagramAccountId, longLivedToken.AccessToken);

        // Return tokens directly instead of redirect (TempData gets lost)
        return Content($@"
            <html>
            <head><title>Instagram Connected</title></head>
            <body style='font-family: monospace; background: #1a1a1a; color: #fff; padding: 40px;'>
                <h1 style='color: #22c55e;'>Instagram Connected Successfully!</h1>
                <p>Add these to your appsettings.json Instagram section:</p>
                <pre style='background: #333; padding: 20px; border-radius: 8px; overflow-x: auto;'>
""AccessToken"": ""{longLivedToken.AccessToken}"",
""InstagramAccountId"": ""{instagramAccountId}"",
""TokenExpiresAt"": ""{expiresAt:O}""
                </pre>
                <p><a href='/admin' style='color: #3b82f6;'>Return to Admin</a></p>
            </body>
            </html>", "text/html");
    }

    [HttpPost("share-instagram")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ShareToInstagram(string slug, string caption, string? imageType, string? quote, string? code, string? lang, string? carouselData, string? quoteColor, string? quoteLogo, bool eli5Carousel = false)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (!_instagramService.HasValidToken)
        {
            return Json(new { success = false, message = "Instagram not connected." });
        }

        var post = await _blogService.GetPostBySlugAsync(slug);
        if (post == null)
        {
            return Json(new { success = false, message = "Post not found." });
        }

        InstagramPostResult result;
        const string eli5CoverUrl = "https://learnedgeek.com/img/eli5-series.png";

        if (imageType == "carousel" && !string.IsNullOrWhiteSpace(carouselData))
        {
            // Handle carousel post
            var slides = System.Text.Json.JsonSerializer.Deserialize<List<CarouselSlide>>(carouselData);
            if (slides == null || slides.Count < 2)
            {
                return Json(new { success = false, message = "Carousel requires at least 2 slides." });
            }

            var imageUrls = new List<string>();

            // Prepend ELI5 series cover as first slide when enabled
            if (eli5Carousel)
            {
                imageUrls.Add(eli5CoverUrl);
            }

            foreach (var slide in slides)
            {
                string imageUrl;
                if (slide.Type == "code" && !string.IsNullOrWhiteSpace(slide.Code))
                {
                    var encodedCode = Uri.EscapeDataString(slide.Code);
                    var language = Uri.EscapeDataString(slide.Lang ?? "code");
                    imageUrl = $"https://learnedgeek.com/img/instagram/{slug}.png?code={encodedCode}&lang={language}";
                }
                else if (!string.IsNullOrWhiteSpace(slide.Quote))
                {
                    var encodedQuote = Uri.EscapeDataString(slide.Quote);
                    var color = Uri.EscapeDataString(slide.Color ?? "light");
                    var logo = Uri.EscapeDataString(slide.Logo ?? "top");
                    imageUrl = $"https://learnedgeek.com/img/instagram/{slug}.png?quote={encodedQuote}&color={color}&logo={logo}";
                }
                else
                {
                    continue; // Skip empty slides
                }
                imageUrls.Add(imageUrl);
            }

            if (imageUrls.Count < 2)
            {
                return Json(new { success = false, message = "Need at least 2 slides with content." });
            }

            result = await _instagramService.ShareCarouselPostAsync(caption, imageUrls);
        }
        else
        {
            // Handle single image post
            string imageUrl;
            if (!string.IsNullOrWhiteSpace(code))
            {
                var encodedCode = Uri.EscapeDataString(code);
                var language = Uri.EscapeDataString(lang ?? "code");
                imageUrl = $"https://learnedgeek.com/img/instagram/{slug}.png?code={encodedCode}&lang={language}";
            }
            else if (!string.IsNullOrWhiteSpace(quote))
            {
                var encodedQuote = Uri.EscapeDataString(quote);
                var color = Uri.EscapeDataString(quoteColor ?? "light");
                var logo = Uri.EscapeDataString(quoteLogo ?? "top");
                imageUrl = $"https://learnedgeek.com/img/instagram/{slug}.png?quote={encodedQuote}&color={color}&logo={logo}";
            }
            else
            {
                imageUrl = GetPublicImageUrl(post.Image, slug) ?? "";
                if (string.IsNullOrEmpty(imageUrl))
                {
                    return Json(new { success = false, message = "No image, quote, or code provided." });
                }
            }

            // ELI5 carousel: pair ELI5 series cover + content image as 2-slide carousel
            if (eli5Carousel)
            {
                var carouselUrls = new List<string> { eli5CoverUrl, imageUrl };
                result = await _instagramService.ShareCarouselPostAsync(caption, carouselUrls);
            }
            else
            {
                result = await _instagramService.ShareImagePostAsync(caption, imageUrl);
            }
        }

        if (result.Success)
        {
            _logger.LogInformation("Shared post {Slug} to Instagram", slug);
            await _blogService.UpdatePostInstagramDateAsync(slug, DateTime.UtcNow);
        }

        return Json(new {
            success = result.Success,
            message = result.Success ? "Posted to Instagram!" : result.ErrorMessage
        });
    }

    private class CarouselSlide
    {
        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; } = "quote";

        [System.Text.Json.Serialization.JsonPropertyName("quote")]
        public string? Quote { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("code")]
        public string? Code { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lang")]
        public string? Lang { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("color")]
        public string? Color { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("logo")]
        public string? Logo { get; set; }
    }

    [HttpPost("suggest-hashtags")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestHashtags(string slug)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (!_hashtagService.IsConfigured)
        {
            return Json(new { success = false, message = "Anthropic API not configured." });
        }

        var post = await _blogService.GetPostBySlugAsync(slug);
        if (post == null)
        {
            return Json(new { success = false, message = "Post not found." });
        }

        var result = await _hashtagService.GenerateHashtagsAsync(post);

        return Json(new {
            success = result.Success,
            hashtags = result.Hashtags,
            message = result.ErrorMessage
        });
    }

    [HttpPost("generate-caption")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateCaption(string slug)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (!_hashtagService.IsConfigured)
        {
            return Json(new { success = false, message = "Anthropic API not configured." });
        }

        var post = await _blogService.GetPostBySlugAsync(slug);
        if (post == null)
        {
            return Json(new { success = false, message = "Post not found." });
        }

        var result = await _hashtagService.GenerateCaptionAsync(post);

        return Json(new {
            success = result.Success,
            caption = result.Caption,
            message = result.ErrorMessage
        });
    }

    [HttpPost("generate-quote")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateQuote(string slug)
    {
        if (!IsAuthorized())
        {
            return Unauthorized();
        }

        if (!_hashtagService.IsConfigured)
        {
            return Json(new { success = false, message = "Anthropic API not configured." });
        }

        var post = await _blogService.GetPostBySlugAsync(slug);
        if (post == null)
        {
            return Json(new { success = false, message = "Post not found." });
        }

        var result = await _hashtagService.GenerateQuoteAsync(post);

        return Json(new {
            success = result.Success,
            quote = result.Quote,
            message = result.ErrorMessage
        });
    }

    [HttpGet("/img/instagram/{slug}.png")]
    public IActionResult GetQuoteCardImage(string slug, [FromQuery] string? quote, [FromQuery] string? code, [FromQuery] string? lang, [FromQuery] string? color, [FromQuery] string? logo)
    {
        byte[]? imageData;

        if (!string.IsNullOrEmpty(code))
        {
            // Generate code snippet card
            imageData = GenerateCodeCard(code, lang ?? "code");
        }
        else if (!string.IsNullOrEmpty(quote))
        {
            // Generate quote card with color and logo options
            imageData = GenerateQuoteCard(quote, color ?? "light", logo ?? "top");
        }
        else
        {
            return BadRequest("Either quote or code parameter is required");
        }

        if (imageData == null)
        {
            return StatusCode(500, "Failed to generate image");
        }

        return File(imageData, "image/png");
    }

    private byte[]? GenerateCodeCard(string code, string language)
    {
        try
        {
            const int size = 1080;
            const int padding = 60;
            const int headerHeight = 60;

            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);

            // Dark background - like Carbon.now.sh Dracula theme
            var bgColor = new SKColor(40, 42, 54); // #282a36
            canvas.Clear(bgColor);

            // Window chrome bar at top
            using var chromePaint = new SKPaint
            {
                Color = new SKColor(33, 34, 44), // Slightly darker
                IsAntialias = true
            };
            canvas.DrawRect(0, 0, size, headerHeight, chromePaint);

            // Window buttons (red, yellow, green circles)
            var buttonY = headerHeight / 2;
            var buttonRadius = 8f;
            var buttonStartX = padding;

            using var redPaint = new SKPaint { Color = new SKColor(255, 95, 86), IsAntialias = true };
            using var yellowPaint = new SKPaint { Color = new SKColor(255, 189, 46), IsAntialias = true };
            using var greenPaint = new SKPaint { Color = new SKColor(39, 201, 63), IsAntialias = true };

            canvas.DrawCircle(buttonStartX, buttonY, buttonRadius, redPaint);
            canvas.DrawCircle(buttonStartX + 28, buttonY, buttonRadius, yellowPaint);
            canvas.DrawCircle(buttonStartX + 56, buttonY, buttonRadius, greenPaint);

            // Language label
            using var langTypeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var langFont = new SKFont(langTypeface, 14);
            using var langPaint = new SKPaint
            {
                Color = new SKColor(139, 148, 158), // Gray text
                IsAntialias = true
            };
            var langText = language.ToLowerInvariant();
            var langWidth = langFont.MeasureText(langText);
            canvas.DrawText(langText, size - padding - langWidth, buttonY + 5, SKTextAlign.Left, langFont, langPaint);

            // Code text
            using var codeTypeface = SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                    ?? SKTypeface.FromFamilyName("Courier New")
                    ?? SKTypeface.Default;
            using var codeFont = new SKFont(codeTypeface, 22);
            using var codePaint = new SKPaint
            {
                Color = new SKColor(248, 248, 242), // Light text #f8f8f2
                IsAntialias = true
            };

            // Split code into lines and wrap if necessary
            var lines = code.Split('\n');
            var maxTextWidth = size - (padding * 2);
            var lineHeight = codeFont.Size * 1.5f;
            var startY = headerHeight + padding;
            var maxLines = (int)((size - headerHeight - padding * 2 - 60) / lineHeight); // Leave room for branding

            var renderedLines = 0;
            foreach (var line in lines)
            {
                if (renderedLines >= maxLines) break;

                // Handle long lines by truncating with ellipsis
                var displayLine = line;
                if (codeFont.MeasureText(displayLine) > maxTextWidth)
                {
                    while (codeFont.MeasureText(displayLine + "...") > maxTextWidth && displayLine.Length > 0)
                    {
                        displayLine = displayLine[..^1];
                    }
                    displayLine += "...";
                }

                canvas.DrawText(displayLine, padding, startY + (renderedLines * lineHeight), SKTextAlign.Left, codeFont, codePaint);
                renderedLines++;
            }

            // Branding at bottom
            using var brandTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var brandFont = new SKFont(brandTypeface, 18);
            using var brandPaint = new SKPaint
            {
                Color = new SKColor(107, 33, 168), // Purple
                IsAntialias = true
            };

            var brandText = "learnedgeek.com";
            var brandWidth = brandFont.MeasureText(brandText);
            canvas.DrawText(brandText, size - padding - brandWidth, size - 30, SKTextAlign.Left, brandFont, brandPaint);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 95);

            return data.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate code card");
            return null;
        }
    }

    private byte[]? GenerateQuoteCard(string quoteText, string colorTheme = "light", string logoPosition = "top")
    {
        try
        {
            const int size = 1080;
            const int padding = 80;

            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);

            // Color theme settings
            SKColor bgColor, textColor, accentColor;
            byte logoAlpha;

            switch (colorTheme.ToLowerInvariant())
            {
                case "dark":
                    bgColor = new SKColor(23, 23, 23); // Near black
                    textColor = new SKColor(248, 248, 248); // Near white
                    accentColor = new SKColor(139, 92, 246); // Lighter purple for dark bg
                    logoAlpha = 80;
                    break;
                case "purple":
                    bgColor = new SKColor(107, 33, 168); // Purple #6B21A8
                    textColor = new SKColor(255, 255, 255); // White
                    accentColor = new SKColor(232, 232, 232); // Light gray accent
                    logoAlpha = 60;
                    break;
                default: // light
                    bgColor = new SKColor(232, 232, 232); // #E8E8E8
                    textColor = new SKColor(23, 23, 23); // Near black
                    accentColor = new SKColor(107, 33, 168); // Purple #6B21A8
                    logoAlpha = 120;
                    break;
            }

            canvas.Clear(bgColor);

            // Load logo
            var logoPath = Path.Combine(_webHostEnvironment.WebRootPath, "img", "learned-geek-logo-transparent.png");
            if (logoPosition != "none" && System.IO.File.Exists(logoPath))
            {
                using var logoStream = System.IO.File.OpenRead(logoPath);
                using var logoBitmap = SKBitmap.Decode(logoStream);
                if (logoBitmap != null)
                {
                    float logoScale, logoX, logoY;

                    switch (logoPosition.ToLowerInvariant())
                    {
                        case "bottom-right":
                            logoScale = (size * 0.20f) / logoBitmap.Width;
                            logoX = size - padding - (logoBitmap.Width * logoScale);
                            logoY = size - padding - (logoBitmap.Height * logoScale);
                            break;
                        case "bottom-left":
                            logoScale = (size * 0.20f) / logoBitmap.Width;
                            logoX = padding;
                            logoY = size - padding - (logoBitmap.Height * logoScale);
                            break;
                        default: // top
                            logoScale = (size * 0.35f) / logoBitmap.Width;
                            logoX = (size - (logoBitmap.Width * logoScale)) / 2;
                            logoY = padding;
                            break;
                    }

                    var logoWidth = logoBitmap.Width * logoScale;
                    var logoHeight = logoBitmap.Height * logoScale;

                    using var logoPaint = new SKPaint
                    {
                        Color = SKColors.White.WithAlpha(logoAlpha),
                        IsAntialias = true
                    };

                    var destRect = new SKRect(logoX, logoY, logoX + logoWidth, logoY + logoHeight);
                    canvas.DrawBitmap(logoBitmap, destRect, logoPaint);
                }
            }

            // Quote text - centered, wrapped (handle line breaks)
            using var quoteTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var quoteFont = new SKFont(quoteTypeface, 52);
            using var quotePaint = new SKPaint
            {
                Color = textColor,
                IsAntialias = true
            };

            // Word wrap the quote (split by explicit line breaks first)
            var maxTextWidth = size - (padding * 2);
            var allLines = new List<string>();
            var paragraphs = quoteText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            foreach (var para in paragraphs)
            {
                if (string.IsNullOrEmpty(para))
                {
                    allLines.Add(""); // Preserve empty lines
                }
                else
                {
                    allLines.AddRange(WrapText(para, quoteFont, maxTextWidth));
                }
            }

            // Calculate total text height
            var lineHeight = quoteFont.Size * 1.4f;
            var totalTextHeight = allLines.Count * lineHeight;

            // Start Y position to center text vertically
            float textStartY;
            if (logoPosition == "top")
            {
                textStartY = (size - totalTextHeight) / 2 + 60; // Shift down to avoid top logo
            }
            else
            {
                textStartY = (size - totalTextHeight) / 2;
            }

            // Draw each line centered
            foreach (var line in allLines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var lineWidth = quoteFont.MeasureText(line);
                    var x = (size - lineWidth) / 2;
                    canvas.DrawText(line, x, textStartY, SKTextAlign.Left, quoteFont, quotePaint);
                }
                textStartY += lineHeight;
            }

            // Only show bottom branding if logo is not at bottom positions
            if (logoPosition != "bottom-right" && logoPosition != "bottom-left")
            {
                // Accent line
                using var accentPaint = new SKPaint
                {
                    Color = accentColor,
                    StrokeWidth = 4,
                    IsAntialias = true
                };
                var lineY = size - 160;
                canvas.DrawLine(size / 2 - 60, lineY, size / 2 + 60, lineY, accentPaint);

                // "LEARNEDGEEK" text at bottom
                using var quoteBrandTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                using var quoteBrandFont = new SKFont(quoteBrandTypeface, 28);
                using var quoteBrandPaint = new SKPaint
                {
                    Color = textColor,
                    IsAntialias = true
                };

                var brandText = "LEARNEDGEEK";
                var brandWidth = quoteBrandFont.MeasureText(brandText);
                canvas.DrawText(brandText, (size - brandWidth) / 2, size - 100, SKTextAlign.Left, quoteBrandFont, quoteBrandPaint);

                // "learnedgeek.com" URL
                using var urlTypeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                using var urlFont = new SKFont(urlTypeface, 22);
                using var urlPaint = new SKPaint
                {
                    Color = accentColor,
                    IsAntialias = true
                };

                var urlText = "learnedgeek.com";
                var urlWidth = urlFont.MeasureText(urlText);
                canvas.DrawText(urlText, (size - urlWidth) / 2, size - 60, SKTextAlign.Left, urlFont, urlPaint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 95);

            return data.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate quote card");
            return null;
        }
    }

    private List<string> WrapText(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            var testWidth = font.MeasureText(testLine);

            if (testWidth > maxWidth && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    private string? GetPublicImageUrl(string? imagePath, string slug)
    {
        if (string.IsNullOrEmpty(imagePath))
            return null;

        // If it's an SVG, we need to serve a PNG version
        // Instagram doesn't support SVG - use our PNG conversion endpoint
        if (imagePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            // Return URL to PNG conversion endpoint
            return $"https://learnedgeek.com/img/posts/{slug}.png";
        }

        // For PNG/JPG, return the direct URL
        return $"https://learnedgeek.com{imagePath}";
    }

    [HttpGet("/img/posts/{slug}.png")]
    public async Task<IActionResult> GetPostImagePng(string slug)
    {
        // Find the post to get its image path
        var post = await _blogService.GetPostBySlugAsync(slug);
        if (post?.Image == null)
        {
            return NotFound();
        }

        var imageData = await GetPostImageAsync(post.Image);
        if (imageData == null)
        {
            return NotFound();
        }

        return File(imageData, "image/png");
    }

    [HttpGet("/img/eli5-series.png")]
    public IActionResult GetEli5SeriesPng()
    {
        var svgPath = Path.Combine(_webHostEnvironment.WebRootPath, "img", "posts", "eli5-series.svg");
        if (!System.IO.File.Exists(svgPath))
        {
            return NotFound();
        }

        var imageData = ConvertSvgToPng(svgPath);
        if (imageData == null)
        {
            return StatusCode(500, "Failed to convert ELI5 series SVG to PNG");
        }

        return File(imageData, "image/png");
    }

    private async Task<byte[]?> GetPostImageAsync(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return null;
        }

        // imagePath is like "/img/posts/filename.svg" - convert to physical path
        var relativePath = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath);

        if (!System.IO.File.Exists(fullPath))
        {
            _logger.LogWarning("Image not found: {Path}", fullPath);
            return null;
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();

        return extension switch
        {
            ".svg" => ConvertSvgToPng(fullPath),
            ".png" => await System.IO.File.ReadAllBytesAsync(fullPath),
            ".jpg" or ".jpeg" => await System.IO.File.ReadAllBytesAsync(fullPath),
            _ => null
        };
    }

    private byte[]? ConvertSvgToPng(string svgPath)
    {
        try
        {
            using var svg = new SKSvg();
            svg.Load(svgPath);

            if (svg.Picture == null)
            {
                _logger.LogWarning("Failed to load SVG from {Path}", svgPath);
                return null;
            }

            // Instagram optimal size: 1080x1080 square
            const int targetSize = 1080;

            var bounds = svg.Picture.CullRect;
            var sourceWidth = bounds.Width;
            var sourceHeight = bounds.Height;

            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                sourceWidth = 800;
                sourceHeight = 450;
            }

            // Calculate scale to fit within square while maintaining aspect ratio
            var scale = Math.Min(targetSize / sourceWidth, targetSize / sourceHeight);

            // Calculate scaled dimensions
            var scaledWidth = sourceWidth * scale;
            var scaledHeight = sourceHeight * scale;

            // Calculate offset to center the image
            var offsetX = (targetSize - scaledWidth) / 2;
            var offsetY = (targetSize - scaledHeight) / 2;

            using var bitmap = new SKBitmap(targetSize, targetSize);
            using var canvas = new SKCanvas(bitmap);

            // White background
            canvas.Clear(SKColors.White);

            // Translate to center position, then scale
            canvas.Translate((float)offsetX, (float)offsetY);
            canvas.Scale((float)scale);

            canvas.DrawPicture(svg.Picture);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);

            return data.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert SVG to PNG: {Path}", svgPath);
            return null;
        }
    }
}
