using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LearnedGeek.Models;
using LearnedGeek.Services;

namespace LearnedGeek.Controllers;

public class HomeController : Controller
{
    private readonly IEmailService _emailService;
    private readonly IRecaptchaService _recaptchaService;
    private readonly RecaptchaSettings _recaptchaSettings;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IEmailService emailService,
        IRecaptchaService recaptchaService,
        IOptions<RecaptchaSettings> recaptchaSettings,
        ILogger<HomeController> logger)
    {
        _emailService = emailService;
        _recaptchaService = recaptchaService;
        _recaptchaSettings = recaptchaSettings.Value;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Work()
    {
        return View();
    }

    public IActionResult Writing()
    {
        return View();
    }

    public IActionResult Services()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Contact()
    {
        ViewBag.RecaptchaSiteKey = _recaptchaSettings.SiteKey;
        return View(new ContactFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactFormModel model)
    {
        ViewBag.RecaptchaSiteKey = _recaptchaSettings.SiteKey;

        // Check honeypot field - bots often fill all fields
        if (!string.IsNullOrWhiteSpace(model.Website))
        {
            _logger.LogWarning("Contact form honeypot triggered - likely bot submission");
            // Return success to not tip off the bot, but don't process
            TempData["ContactSuccess"] = true;
            return RedirectToAction(nameof(Contact));
        }

        // Validate reCAPTCHA
        var recaptchaResult = await _recaptchaService.ValidateAsync(model.RecaptchaToken);
        if (!recaptchaResult.Success)
        {
            _logger.LogWarning("reCAPTCHA validation failed: {Error}", recaptchaResult.ErrorMessage);
            ModelState.AddModelError(string.Empty,
                recaptchaResult.ErrorMessage ?? "Security verification failed. Please try again.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var success = await _emailService.SendContactEmailAsync(model);

        if (success)
        {
            TempData["ContactSuccess"] = true;
            return RedirectToAction(nameof(Contact));
        }

        ModelState.AddModelError(string.Empty, "There was an error sending your message. Please try again later.");
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCode(int? code)
    {
        var statusCode = code ?? 500;
        Response.StatusCode = statusCode;

        ViewBag.StatusCode = statusCode;
        ViewBag.StatusMessage = statusCode switch
        {
            404 => "Page Not Found",
            403 => "Forbidden",
            500 => "Server Error",
            _ => "Error"
        };
        ViewBag.StatusDescription = statusCode switch
        {
            404 => "The page you're looking for doesn't exist or has been moved.",
            403 => "You don't have permission to access this resource.",
            500 => "Something went wrong on our end. Please try again later.",
            _ => "An unexpected error occurred."
        };

        return View();
    }
}
