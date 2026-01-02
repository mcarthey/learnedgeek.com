using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using LearnedGeek.Models;
using LearnedGeek.Services;

namespace LearnedGeek.Controllers;

public class HomeController : Controller
{
    private readonly IEmailService _emailService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IEmailService emailService, ILogger<HomeController> logger)
    {
        _emailService = emailService;
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

    [HttpGet]
    public IActionResult Contact()
    {
        return View(new ContactFormModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactFormModel model)
    {
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
}
