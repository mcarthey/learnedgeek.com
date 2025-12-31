using LearnedGeek.Models;

namespace LearnedGeek.Services;

public interface IEmailService
{
    Task<bool> SendContactEmailAsync(ContactFormModel model);
}
