using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using LearnedGeek.Models;

namespace LearnedGeek.Services;

public interface ILinkedInService
{
    string GetAuthorizationUrl(string state);
    Task<LinkedInTokenResponse?> ExchangeCodeForTokenAsync(string code);
    Task<string?> GetMemberIdAsync(string accessToken);
    Task<LinkedInPostResult> SharePostAsync(string text, string articleUrl);
    bool IsConfigured { get; }
    bool HasValidToken { get; }
}

public class LinkedInTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public int RefreshTokenExpiresIn { get; set; }
}

public class LinkedInPostResult
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LinkedInService : ILinkedInService
{
    private readonly LinkedInSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LinkedInService> _logger;

    public LinkedInService(
        HttpClient httpClient,
        IOptions<LinkedInSettings> settings,
        ILogger<LinkedInService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrEmpty(_settings.ClientId) &&
        !string.IsNullOrEmpty(_settings.ClientSecret);

    public bool HasValidToken =>
        !string.IsNullOrEmpty(_settings.AccessToken) &&
        !string.IsNullOrEmpty(_settings.MemberId);

    public string GetAuthorizationUrl(string state)
    {
        var scopes = "openid profile w_member_social";
        return $"https://www.linkedin.com/oauth/v2/authorization" +
               $"?response_type=code" +
               $"&client_id={_settings.ClientId}" +
               $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
               $"&state={state}" +
               $"&scope={Uri.EscapeDataString(scopes)}";
    }

    public async Task<LinkedInTokenResponse?> ExchangeCodeForTokenAsync(string code)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _settings.ClientId,
            ["client_secret"] = _settings.ClientSecret,
            ["redirect_uri"] = _settings.RedirectUri
        });

        try
        {
            var response = await _httpClient.PostAsync(
                "https://www.linkedin.com/oauth/v2/accessToken",
                content);

            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("LinkedIn token exchange failed: {Response}", json);
                return null;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new LinkedInTokenResponse
            {
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                ExpiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 0,
                RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "",
                RefreshTokenExpiresIn = root.TryGetProperty("refresh_token_expires_in", out var rtExp) ? rtExp.GetInt32() : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for token");
            return null;
        }
    }

    public async Task<string?> GetMemberIdAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.linkedin.com/v2/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get member info: {Response}", json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("sub").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting member ID");
            return null;
        }
    }

    public async Task<LinkedInPostResult> SharePostAsync(string text, string articleUrl)
    {
        if (!HasValidToken)
        {
            return new LinkedInPostResult
            {
                Success = false,
                ErrorMessage = "LinkedIn is not connected. Please authorize first."
            };
        }

        // LinkedIn's API uses a specific format for type discriminators
        var properPayload = $$"""
        {
            "author": "urn:li:person:{{_settings.MemberId}}",
            "lifecycleState": "PUBLISHED",
            "specificContent": {
                "com.linkedin.ugc.ShareContent": {
                    "shareCommentary": {
                        "text": {{JsonSerializer.Serialize(text)}}
                    },
                    "shareMediaCategory": "ARTICLE",
                    "media": [
                        {
                            "status": "READY",
                            "originalUrl": {{JsonSerializer.Serialize(articleUrl)}}
                        }
                    ]
                }
            },
            "visibility": {
                "com.linkedin.ugc.MemberNetworkVisibility": "PUBLIC"
            }
        }
        """;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/v2/ugcPosts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);
            request.Headers.Add("X-Restli-Protocol-Version", "2.0.0");
            request.Content = new StringContent(properPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted to LinkedIn");

                // Extract post ID from response header
                var postId = response.Headers.TryGetValues("x-restli-id", out var ids)
                    ? ids.FirstOrDefault()
                    : null;

                return new LinkedInPostResult { Success = true, PostId = postId };
            }
            else
            {
                _logger.LogError("LinkedIn post failed: {Response}", responseJson);
                return new LinkedInPostResult
                {
                    Success = false,
                    ErrorMessage = $"LinkedIn API error: {responseJson}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting to LinkedIn");
            return new LinkedInPostResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
