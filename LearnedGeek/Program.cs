using LearnedGeek.Models;
using LearnedGeek.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();

// Configure reCAPTCHA settings
builder.Services.Configure<RecaptchaSettings>(builder.Configuration.GetSection("Recaptcha"));
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>();

// Blog service
builder.Services.AddSingleton<IBlogService, BlogService>();

// LinkedIn integration
builder.Services.Configure<LinkedInSettings>(builder.Configuration.GetSection("LinkedIn"));
builder.Services.AddHttpClient<ILinkedInService, LinkedInService>();

// Admin settings
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Admin"));

// Session for OAuth state
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
