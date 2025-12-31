using System.Net;
using System.Net.Mail;
using LearnedGeek.Models;
using Microsoft.Extensions.Options;

namespace LearnedGeek.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendContactEmailAsync(ContactFormModel model)
    {
        try
        {
            using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
            {
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = $"Contact Form: {model.Subject}",
                IsBodyHtml = true,
                Body = BuildEmailBody(model)
            };

            mailMessage.To.Add(_settings.RecipientEmail);
            mailMessage.ReplyToList.Add(new MailAddress(model.Email, model.Name));

            await client.SendMailAsync(mailMessage);

            _logger.LogInformation("Contact email sent successfully from {Email}", model.Email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send contact email from {Email}", model.Email);
            return false;
        }
    }

    private static string BuildEmailBody(ContactFormModel model)
    {
        var name = System.Web.HttpUtility.HtmlEncode(model.Name);
        var email = System.Web.HttpUtility.HtmlEncode(model.Email);
        var subject = System.Web.HttpUtility.HtmlEncode(model.Subject);
        var message = System.Web.HttpUtility.HtmlEncode(model.Message).Replace("\n", "<br>");

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {left} font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif; line-height: 1.6; color: #333; {right}
                    .container {left} max-width: 600px; margin: 0 auto; padding: 20px; {right}
                    .header {left} border-bottom: 1px solid #e5e5e5; padding-bottom: 20px; margin-bottom: 20px; {right}
                    .field {left} margin-bottom: 16px; {right}
                    .label {left} font-weight: 600; color: #666; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; {right}
                    .value {left} margin-top: 4px; {right}
                    .message {left} background: #f9f9f9; padding: 16px; border-left: 3px solid #000; {right}
                </style>
            </head>
            <body>
                <div class="container">
                    <div class="header">
                        <h2 style="margin: 0;">New Contact Form Submission</h2>
                    </div>
                    <div class="field">
                        <div class="label">From</div>
                        <div class="value">{name} ({email})</div>
                    </div>
                    <div class="field">
                        <div class="label">Subject</div>
                        <div class="value">{subject}</div>
                    </div>
                    <div class="field">
                        <div class="label">Message</div>
                        <div class="message">{message}</div>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private const string left = "{";
    private const string right = "}";
}
