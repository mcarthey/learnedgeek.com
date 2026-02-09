using System.Text;
using System.Text.Json;
using LearnedGeek.Models;

namespace LearnedGeek.Services;

public interface IHashtagService
{
    Task<HashtagResult> GenerateHashtagsAsync(BlogPost post);
    Task<CaptionResult> GenerateCaptionAsync(BlogPost post);
    Task<QuoteResult> GenerateQuoteAsync(BlogPost post);
    bool IsConfigured { get; }
}

public class HashtagResult
{
    public bool Success { get; set; }
    public List<string> Hashtags { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class CaptionResult
{
    public bool Success { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class QuoteResult
{
    public bool Success { get; set; }
    public string Quote { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class AnthropicSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class HashtagService : IHashtagService
{
    private readonly AnthropicSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<HashtagService> _logger;
    private const string AnthropicApiBase = "https://api.anthropic.com/v1/messages";

    public HashtagService(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<AnthropicSettings> settings,
        ILogger<HashtagService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_settings.ApiKey);

    public async Task<HashtagResult> GenerateHashtagsAsync(BlogPost post)
    {
        if (!IsConfigured)
        {
            return new HashtagResult
            {
                Success = false,
                ErrorMessage = "Anthropic API key not configured."
            };
        }

        try
        {
            var prompt = BuildPrompt(post);

            var requestBody = new
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 500,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicApiBase);
            request.Headers.Add("x-api-key", _settings.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error: {Response}", responseJson);
                return new HashtagResult
                {
                    Success = false,
                    ErrorMessage = $"API error: {response.StatusCode}"
                };
            }

            // Parse the response
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                return new HashtagResult
                {
                    Success = false,
                    ErrorMessage = "Empty response from API"
                };
            }

            // Parse hashtags from the response
            var hashtags = ParseHashtags(content);

            _logger.LogInformation("Generated {Count} hashtags for post {Slug}", hashtags.Count, post.Slug);

            return new HashtagResult
            {
                Success = true,
                Hashtags = hashtags
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating hashtags");
            return new HashtagResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<CaptionResult> GenerateCaptionAsync(BlogPost post)
    {
        if (!IsConfigured)
        {
            return new CaptionResult
            {
                Success = false,
                ErrorMessage = "Anthropic API key not configured."
            };
        }

        try
        {
            var prompt = BuildCaptionPrompt(post);

            var requestBody = new
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 1000,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicApiBase);
            request.Headers.Add("x-api-key", _settings.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error: {Response}", responseJson);
                return new CaptionResult
                {
                    Success = false,
                    ErrorMessage = $"API error: {response.StatusCode}"
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                return new CaptionResult
                {
                    Success = false,
                    ErrorMessage = "Empty response from API"
                };
            }

            _logger.LogInformation("Generated caption for post {Slug}", post.Slug);

            return new CaptionResult
            {
                Success = true,
                Caption = content.Trim()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating caption");
            return new CaptionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<QuoteResult> GenerateQuoteAsync(BlogPost post)
    {
        if (!IsConfigured)
        {
            return new QuoteResult
            {
                Success = false,
                ErrorMessage = "Anthropic API key not configured."
            };
        }

        try
        {
            var prompt = BuildQuotePrompt(post);

            var requestBody = new
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 200,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicApiBase);
            request.Headers.Add("x-api-key", _settings.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error: {Response}", responseJson);
                return new QuoteResult
                {
                    Success = false,
                    ErrorMessage = $"API error: {response.StatusCode}"
                };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(content))
            {
                return new QuoteResult
                {
                    Success = false,
                    ErrorMessage = "Empty response from API"
                };
            }

            _logger.LogInformation("Generated quote for post {Slug}", post.Slug);

            return new QuoteResult
            {
                Success = true,
                Quote = content.Trim().Trim('"') // Remove any surrounding quotes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quote");
            return new QuoteResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string BuildQuotePrompt(BlogPost post)
    {
        return $"""
            Extract or create a compelling quote for an Instagram quote card based on this blog post.

            Blog post details:
            - Title: {post.Title}
            - Description: {post.Description}
            - Tags: {string.Join(", ", post.Tags)}

            Requirements:
            1. The quote should be 50-120 characters (short enough to display large on an image)
            2. It should be a single powerful statement, insight, or question
            3. It should make people want to learn more (click to read the full post)
            4. No hashtags, no emojis
            5. Can be a direct insight from the topic or a thought-provoking question

            Return ONLY the quote text. No quotation marks, no attribution, no explanations.
            """;
    }

    private string BuildCaptionPrompt(BlogPost post)
    {
        var categoryContext = post.Category switch
        {
            Category.Tech => "software development and technology",
            Category.Writing => "writing and content creation",
            Category.Gaming => "gaming, game development, and data mining",
            Category.Project => "hands-on projects and DIY",
            Category.Personal => "personal stories and reflections",
            _ => "general interest"
        };

        return $"""
            Write an engaging Instagram caption for a blog post. The caption should:
            1. Start with a compelling hook that grabs attention (question, bold statement, or relatable observation)
            2. Provide value or insight related to the post topic
            3. Include a clear call-to-action (e.g., "Link in bio", "Save this for later", etc.)
            4. Be conversational and authentic in tone - but PROFESSIONAL (this is a technical blog)
            5. Be 150-300 characters (before hashtags)
            6. NOT include any hashtags - those will be added separately
            7. IMPORTANT: Do NOT use any emojis. This is a professional technical blog - keep it clean and text-only.

            Blog post details:
            - Title: {post.Title}
            - Description: {post.Description}
            - Category: {categoryContext}
            - Tags: {string.Join(", ", post.Tags)}

            Write ONLY the caption text. No quotes, no explanations, no hashtags, NO EMOJIS.
            """;
    }

    private string BuildPrompt(BlogPost post)
    {
        var categoryContext = post.Category switch
        {
            Category.Tech => "software development, programming, and technology",
            Category.Writing => "writing, creativity, and content creation",
            Category.Gaming => "gaming, game development, and data mining",
            Category.Project => "hands-on projects, DIY, and tinkering",
            Category.Personal => "personal stories, reflections, and life",
            _ => "general interest topics"
        };

        return $"""
            Generate Instagram hashtags for a blog post.

            Blog post details:
            - Title: {post.Title}
            - Description: {post.Description}
            - Category: {post.Category} ({categoryContext})
            - Tags: {string.Join(", ", post.Tags)}

            Requirements:
            1. Generate exactly 8-10 hashtags total (quality over quantity)
            2. Include 2-3 high-volume hashtags for discoverability
            3. Include 3-4 medium-volume hashtags for the specific subject
            4. Include 3-4 niche hashtags that directly relate to the content
            5. All hashtags should be lowercase, no spaces
            6. No generic spam hashtags like #followforfollow
            7. Focus on hashtags that tech professionals and developers would follow

            Format: Return the hashtags in groups separated by blank lines, like this:
            #hashtag1 #hashtag2 #hashtag3

            #hashtag4 #hashtag5 #hashtag6

            #hashtag7 #hashtag8

            This helps with readability. Return ONLY the hashtags in this format. No explanations.
            """;
    }

    private List<string> ParseHashtags(string content)
    {
        var hashtags = new List<string>();

        // Split by whitespace and newlines to find all hashtags
        var tokens = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (trimmed.StartsWith('#'))
            {
                // Clean up the hashtag
                var hashtag = trimmed.ToLowerInvariant();

                // Remove any non-alphanumeric characters except #
                hashtag = new string(hashtag.Where(c => c == '#' || char.IsLetterOrDigit(c)).ToArray());

                if (hashtag.Length > 1 && !hashtags.Contains(hashtag))
                {
                    hashtags.Add(hashtag);
                }
            }
        }

        // Limit to 12 hashtags (we're going for quality over quantity now)
        return hashtags.Take(12).ToList();
    }
}
