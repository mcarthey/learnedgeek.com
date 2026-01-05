using System.ComponentModel.DataAnnotations;

namespace LearnedGeek.Models;

public class ContactFormModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Subject is required")]
    [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message is required")]
    [StringLength(5000, ErrorMessage = "Message cannot exceed 5000 characters")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Google reCAPTCHA token - populated by the client-side reCAPTCHA widget
    /// </summary>
    public string RecaptchaToken { get; set; } = string.Empty;

    /// <summary>
    /// Honeypot field - should be empty for legitimate submissions.
    /// Bots often fill all fields, triggering this trap.
    /// </summary>
    public string? Website { get; set; }
}
