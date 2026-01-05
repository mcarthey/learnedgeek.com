using System.Text.Json;
using System.Text.Json.Serialization;
using LearnedGeek.Models;
using Microsoft.Extensions.Options;

namespace LearnedGeek.Services;

/// <summary>
/// Service interface for Google reCAPTCHA validation
/// </summary>
public interface IRecaptchaService
{
    /// <summary>
    /// Validates a reCAPTCHA token
    /// </summary>
    /// <param name="token">The token from the client-side reCAPTCHA</param>
    /// <returns>Validation result with success status and score</returns>
    Task<RecaptchaValidationResult> ValidateAsync(string token);
}

/// <summary>
/// Google reCAPTCHA v3 validation service
/// </summary>
public class RecaptchaService : IRecaptchaService
{
    private readonly RecaptchaSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecaptchaService> _logger;
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    public RecaptchaService(
        IOptions<RecaptchaSettings> settings,
        HttpClient httpClient,
        ILogger<RecaptchaService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<RecaptchaValidationResult> ValidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new RecaptchaValidationResult
            {
                Success = false,
                ErrorMessage = "reCAPTCHA token is required"
            };
        }

        // If secret key is not configured, allow in development
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            _logger.LogWarning("reCAPTCHA secret key not configured - skipping validation");
            return new RecaptchaValidationResult
            {
                Success = true,
                Score = 1.0f,
                Action = "contact_form"
            };
        }

        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", _settings.SecretKey),
                new KeyValuePair<string, string>("response", token)
            });

            var response = await _httpClient.PostAsync(VerifyUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<RecaptchaResponse>(json);

            if (result == null)
            {
                _logger.LogWarning("Failed to deserialize reCAPTCHA response");
                return new RecaptchaValidationResult
                {
                    Success = false,
                    ErrorMessage = "Failed to validate reCAPTCHA"
                };
            }

            // For reCAPTCHA v3, check the score
            if (result.Success && result.Score >= _settings.MinimumScore)
            {
                _logger.LogInformation("reCAPTCHA validation successful. Score: {Score}", result.Score);
                return new RecaptchaValidationResult
                {
                    Success = true,
                    Score = result.Score,
                    Action = result.Action
                };
            }

            _logger.LogWarning("reCAPTCHA validation failed. Success: {Success}, Score: {Score}, Errors: {Errors}",
                result.Success, result.Score, string.Join(", ", result.ErrorCodes ?? Array.Empty<string>()));

            return new RecaptchaValidationResult
            {
                Success = false,
                Score = result.Score,
                ErrorMessage = result.Score < _settings.MinimumScore
                    ? "Suspicious activity detected. Please try again."
                    : "reCAPTCHA validation failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating reCAPTCHA token");
            return new RecaptchaValidationResult
            {
                Success = false,
                ErrorMessage = "An error occurred during verification"
            };
        }
    }
}

/// <summary>
/// Result of reCAPTCHA validation
/// </summary>
public class RecaptchaValidationResult
{
    public bool Success { get; set; }
    public float Score { get; set; }
    public string? Action { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response from Google reCAPTCHA API
/// </summary>
public class RecaptchaResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("challenge_ts")]
    public string? ChallengeTs { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("error-codes")]
    public string[]? ErrorCodes { get; set; }
}
