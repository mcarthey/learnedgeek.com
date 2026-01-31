using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using LearnedGeek.Models;

namespace LearnedGeek.Services;

public interface IInstagramService
{
    string GetAuthorizationUrl(string state);
    Task<InstagramTokenResponse?> ExchangeCodeForTokenAsync(string code);
    Task<InstagramTokenResponse?> ExchangeLongLivedTokenAsync(string shortLivedToken);
    Task<string?> GetInstagramAccountIdAsync(string accessToken);
    Task<(string? AccountId, string DebugInfo)> GetInstagramAccountIdWithDebugAsync(string accessToken);
    Task<InstagramPostResult> ShareImagePostAsync(string caption, string publicImageUrl);
    Task<InstagramPostResult> ShareCarouselPostAsync(string caption, List<string> publicImageUrls);
    bool IsConfigured { get; }
    bool HasValidToken { get; }
}

public class InstagramTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public long ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
}

public class InstagramPostResult
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class InstagramService : IInstagramService
{
    private readonly InstagramSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<InstagramService> _logger;
    private const string GraphApiBase = "https://graph.facebook.com/v19.0";

    public InstagramService(
        HttpClient httpClient,
        IOptions<InstagramSettings> settings,
        ILogger<InstagramService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_settings.AppId) &&
        !string.IsNullOrEmpty(_settings.AppSecret);

    public bool HasValidToken =>
        !string.IsNullOrEmpty(_settings.AccessToken) &&
        !string.IsNullOrEmpty(_settings.InstagramAccountId) &&
        (_settings.TokenExpiresAt == null || _settings.TokenExpiresAt > DateTime.UtcNow);

    public string GetAuthorizationUrl(string state)
    {
        // Instagram Content Publishing API requires these permissions
        var scopes = "instagram_basic,instagram_content_publish,pages_read_engagement,pages_show_list,business_management";
        return $"https://www.facebook.com/v19.0/dialog/oauth" +
               $"?client_id={_settings.AppId}" +
               $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
               $"&state={state}" +
               $"&scope={Uri.EscapeDataString(scopes)}";
    }

    public async Task<InstagramTokenResponse?> ExchangeCodeForTokenAsync(string code)
    {
        var url = $"{GraphApiBase}/oauth/access_token" +
                  $"?client_id={_settings.AppId}" +
                  $"&client_secret={_settings.AppSecret}" +
                  $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
                  $"&code={code}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram token exchange failed: {Response}", json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new InstagramTokenResponse
            {
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "bearer" : "bearer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for Instagram token");
            return null;
        }
    }

    public async Task<InstagramTokenResponse?> ExchangeLongLivedTokenAsync(string shortLivedToken)
    {
        // Exchange short-lived token (1 hour) for long-lived token (60 days)
        var url = $"{GraphApiBase}/oauth/access_token" +
                  $"?grant_type=fb_exchange_token" +
                  $"&client_id={_settings.AppId}" +
                  $"&client_secret={_settings.AppSecret}" +
                  $"&fb_exchange_token={shortLivedToken}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram long-lived token exchange failed: {Response}", json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new InstagramTokenResponse
            {
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                ExpiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt64() : 5184000, // 60 days default
                TokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "bearer" : "bearer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging for long-lived Instagram token");
            return null;
        }
    }

    public async Task<string?> GetInstagramAccountIdAsync(string accessToken)
    {
        return (await GetInstagramAccountIdWithDebugAsync(accessToken)).AccountId;
    }

    public async Task<(string? AccountId, string DebugInfo)> GetInstagramAccountIdWithDebugAsync(string accessToken)
    {
        var debug = new System.Text.StringBuilder();
        try
        {
            // Step 1: Get Facebook Pages the user manages
            var pagesUrl = $"{GraphApiBase}/me/accounts?access_token={accessToken}";
            var pagesResponse = await _httpClient.GetAsync(pagesUrl);
            var pagesJson = await pagesResponse.Content.ReadAsStringAsync();

            debug.AppendLine($"GET /me/accounts status: {pagesResponse.StatusCode}");
            debug.AppendLine($"Response: {pagesJson}");

            if (!pagesResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get Facebook pages: {Response}", pagesJson);
                return (null, debug.ToString());
            }

            using var pagesDoc = JsonDocument.Parse(pagesJson);
            var pages = pagesDoc.RootElement.GetProperty("data");

            debug.AppendLine($"Pages found: {pages.GetArrayLength()}");

            if (pages.GetArrayLength() == 0)
            {
                _logger.LogWarning("No Facebook pages found for this account");
                debug.AppendLine("ERROR: No Facebook pages returned. User may not have granted page access.");
                return (null, debug.ToString());
            }

            // Get the first page's ID and access token
            var firstPage = pages[0];
            var pageId = firstPage.GetProperty("id").GetString();
            var pageName = firstPage.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "unknown";
            var pageAccessToken = firstPage.GetProperty("access_token").GetString();

            debug.AppendLine($"Using page: {pageName} (ID: {pageId})");
            _logger.LogInformation("Found Facebook page: {PageId} ({PageName})", pageId, pageName);

            // Step 2: Get the Instagram Business Account linked to this page
            var igUrl = $"{GraphApiBase}/{pageId}?fields=instagram_business_account&access_token={pageAccessToken}";
            var igResponse = await _httpClient.GetAsync(igUrl);
            var igJson = await igResponse.Content.ReadAsStringAsync();

            debug.AppendLine($"GET /{pageId}?fields=instagram_business_account status: {igResponse.StatusCode}");
            debug.AppendLine($"Response: {igJson}");

            if (!igResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get Instagram account: {Response}", igJson);
                return (null, debug.ToString());
            }

            using var igDoc = JsonDocument.Parse(igJson);

            if (!igDoc.RootElement.TryGetProperty("instagram_business_account", out var igAccount))
            {
                _logger.LogWarning("No Instagram Business account linked to this Facebook page");
                debug.AppendLine("ERROR: No instagram_business_account field in response.");
                debug.AppendLine("The Instagram account may not be linked to this Page, or may not be a Business/Creator account.");
                return (null, debug.ToString());
            }

            var instagramAccountId = igAccount.GetProperty("id").GetString();
            debug.AppendLine($"SUCCESS: Instagram Account ID: {instagramAccountId}");
            _logger.LogInformation("Found Instagram Business Account: {AccountId}", instagramAccountId);

            return (instagramAccountId, debug.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Instagram account ID");
            debug.AppendLine($"EXCEPTION: {ex.Message}");
            return (null, debug.ToString());
        }
    }

    public async Task<InstagramPostResult> ShareImagePostAsync(string caption, string publicImageUrl)
    {
        if (!HasValidToken)
        {
            return new InstagramPostResult
            {
                Success = false,
                ErrorMessage = "Instagram is not connected. Please authorize first."
            };
        }

        try
        {
            // Step 1: Create a media container
            // Note: Instagram API requires the image to be publicly accessible via URL
            var createUrl = $"{GraphApiBase}/{_settings.InstagramAccountId}/media" +
                           $"?image_url={Uri.EscapeDataString(publicImageUrl)}" +
                           $"&caption={Uri.EscapeDataString(caption)}" +
                           $"&access_token={_settings.AccessToken}";

            _logger.LogInformation("Creating Instagram media container...");

            var createResponse = await _httpClient.PostAsync(createUrl, null);
            var createJson = await createResponse.Content.ReadAsStringAsync();

            if (!createResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram media container creation failed: {Response}", createJson);
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create media container: {createJson}"
                };
            }

            using var createDoc = JsonDocument.Parse(createJson);
            var containerId = createDoc.RootElement.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(containerId))
            {
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = "Failed to get container ID from Instagram"
                };
            }

            _logger.LogInformation("Created media container: {ContainerId}", containerId);

            // Step 2: Wait for container to be ready
            var isReady = await WaitForContainerReadyAsync(containerId);
            if (!isReady)
            {
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = "Media container failed to process in time. Please try again."
                };
            }

            // Step 3: Publish the media container
            var publishUrl = $"{GraphApiBase}/{_settings.InstagramAccountId}/media_publish" +
                            $"?creation_id={containerId}" +
                            $"&access_token={_settings.AccessToken}";

            _logger.LogInformation("Publishing Instagram post...");

            var publishResponse = await _httpClient.PostAsync(publishUrl, null);
            var publishJson = await publishResponse.Content.ReadAsStringAsync();

            if (!publishResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram publish failed: {Response}", publishJson);
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to publish post: {publishJson}"
                };
            }

            using var publishDoc = JsonDocument.Parse(publishJson);
            var postId = publishDoc.RootElement.GetProperty("id").GetString();

            _logger.LogInformation("Successfully posted to Instagram: {PostId}", postId);

            return new InstagramPostResult
            {
                Success = true,
                PostId = postId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting to Instagram");
            return new InstagramPostResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<InstagramPostResult> ShareCarouselPostAsync(string caption, List<string> publicImageUrls)
    {
        if (!HasValidToken)
        {
            return new InstagramPostResult
            {
                Success = false,
                ErrorMessage = "Instagram is not connected. Please authorize first."
            };
        }

        if (publicImageUrls.Count < 2 || publicImageUrls.Count > 10)
        {
            return new InstagramPostResult
            {
                Success = false,
                ErrorMessage = "Carousel requires 2-10 images."
            };
        }

        try
        {
            var childContainerIds = new List<string>();

            // Step 1: Create media containers for each image (no caption on children)
            foreach (var imageUrl in publicImageUrls)
            {
                var createUrl = $"{GraphApiBase}/{_settings.InstagramAccountId}/media" +
                               $"?image_url={Uri.EscapeDataString(imageUrl)}" +
                               $"&is_carousel_item=true" +
                               $"&access_token={_settings.AccessToken}";

                _logger.LogInformation("Creating carousel item container for: {Url}", imageUrl);

                var createResponse = await _httpClient.PostAsync(createUrl, null);
                var createJson = await createResponse.Content.ReadAsStringAsync();

                if (!createResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to create carousel item: {Response}", createJson);
                    return new InstagramPostResult
                    {
                        Success = false,
                        ErrorMessage = $"Failed to create carousel item: {createJson}"
                    };
                }

                using var createDoc = JsonDocument.Parse(createJson);
                var containerId = createDoc.RootElement.GetProperty("id").GetString();

                if (string.IsNullOrEmpty(containerId))
                {
                    return new InstagramPostResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to get container ID for carousel item"
                    };
                }

                childContainerIds.Add(containerId);
                _logger.LogInformation("Created carousel item: {ContainerId}", containerId);
            }

            // Step 2: Create the carousel container
            var childrenParam = string.Join(",", childContainerIds);
            var carouselUrl = $"{GraphApiBase}/{_settings.InstagramAccountId}/media" +
                             $"?media_type=CAROUSEL" +
                             $"&children={childrenParam}" +
                             $"&caption={Uri.EscapeDataString(caption)}" +
                             $"&access_token={_settings.AccessToken}";

            _logger.LogInformation("Creating carousel container with {Count} items...", childContainerIds.Count);

            var carouselResponse = await _httpClient.PostAsync(carouselUrl, null);
            var carouselJson = await carouselResponse.Content.ReadAsStringAsync();

            if (!carouselResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create carousel: {Response}", carouselJson);
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to create carousel: {carouselJson}"
                };
            }

            using var carouselDoc = JsonDocument.Parse(carouselJson);
            var carouselContainerId = carouselDoc.RootElement.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(carouselContainerId))
            {
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = "Failed to get carousel container ID"
                };
            }

            _logger.LogInformation("Created carousel container: {ContainerId}", carouselContainerId);

            // Step 3: Wait for carousel container to be ready
            var isReady = await WaitForContainerReadyAsync(carouselContainerId);
            if (!isReady)
            {
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = "Carousel container failed to process in time. Please try again."
                };
            }

            // Step 4: Publish the carousel
            var publishUrl = $"{GraphApiBase}/{_settings.InstagramAccountId}/media_publish" +
                            $"?creation_id={carouselContainerId}" +
                            $"&access_token={_settings.AccessToken}";

            _logger.LogInformation("Publishing carousel...");

            var publishResponse = await _httpClient.PostAsync(publishUrl, null);
            var publishJson = await publishResponse.Content.ReadAsStringAsync();

            if (!publishResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to publish carousel: {Response}", publishJson);
                return new InstagramPostResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to publish carousel: {publishJson}"
                };
            }

            using var publishDoc = JsonDocument.Parse(publishJson);
            var postId = publishDoc.RootElement.GetProperty("id").GetString();

            _logger.LogInformation("Successfully posted carousel to Instagram: {PostId}", postId);

            return new InstagramPostResult
            {
                Success = true,
                PostId = postId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting carousel to Instagram");
            return new InstagramPostResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Polls Instagram API to check if a media container is ready for publishing.
    /// Instagram requires the container to finish processing before it can be published.
    /// </summary>
    private async Task<bool> WaitForContainerReadyAsync(string containerId, int maxAttempts = 30, int delayMs = 2000)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var statusUrl = $"{GraphApiBase}/{containerId}" +
                               $"?fields=status_code" +
                               $"&access_token={_settings.AccessToken}";

                var response = await _httpClient.GetAsync(statusUrl);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Container status check failed (attempt {Attempt}): {Response}", attempt, json);
                    await Task.Delay(delayMs);
                    continue;
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("status_code", out var statusElement))
                {
                    var status = statusElement.GetString();
                    _logger.LogInformation("Container {ContainerId} status: {Status} (attempt {Attempt})", containerId, status, attempt);

                    switch (status)
                    {
                        case "FINISHED":
                            return true;
                        case "ERROR":
                            _logger.LogError("Container processing failed: {Response}", json);
                            return false;
                        case "IN_PROGRESS":
                            // Still processing, wait and retry
                            await Task.Delay(delayMs);
                            continue;
                        default:
                            _logger.LogWarning("Unknown container status: {Status}", status);
                            await Task.Delay(delayMs);
                            continue;
                    }
                }

                // No status_code field - might already be ready
                _logger.LogInformation("No status_code field, assuming container is ready");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking container status (attempt {Attempt})", attempt);
                await Task.Delay(delayMs);
            }
        }

        _logger.LogError("Container {ContainerId} did not become ready after {MaxAttempts} attempts", containerId, maxAttempts);
        return false;
    }
}
