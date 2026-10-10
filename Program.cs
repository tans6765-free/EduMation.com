using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("sensitive-email", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0, AutoReplenishment = true }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 100L * 1024L * 1024L);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 100L * 1024L * 1024L);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.Configure<OpenAiOptions>(builder.Configuration.GetSection("OpenAI"));
var aiProvider = builder.Configuration["Ai:Provider"] ?? "Gemini";
if (aiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<IAiLearningService, GeminiLearningService>();
else if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<IAiLearningService, OpenAiLearningService>();
else
    throw new InvalidOperationException("Ai:Provider must be set to Gemini or OpenAI.");
builder.Services.Configure<NctbContentOptions>(builder.Configuration.GetSection("Nctb"));
builder.Services.AddSingleton<NctbPdfExtractionService>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AdminProvisioningOptions>(builder.Configuration.GetSection("AdminProvisioning"));
builder.Services.AddTransient<IEmailSender, MailKitEmailSender>();
builder.Services.AddScoped<IStudentEngagementService, StudentEngagementService>();
builder.Services.AddScoped<AdminProvisioner>();
builder.Services.AddScoped<IMediaStorageService, LocalMediaStorageService>();
builder.Services.AddScoped<DevelopmentDataSeeder>();
builder.Services.AddScoped<NctbCatalogSeeder>();
builder.Services.AddScoped<NctbBookCatalogSeeder>();
builder.Services.AddScoped<IAiUsageRecorder, AiUsageRecorder>();
builder.Services.AddHostedService<ReminderDispatcher>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:KeyPath"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys"))).SetApplicationName("EduMation");
builder.Services.AddHttpClient<IGoogleCalendarService, GoogleCalendarService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(3));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// Only role names are provisioned generally. No default administrator is created;
// sample curriculum is added by DevelopmentDataSeeder only in Development.
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        await services.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Tutor", "Editor", "Student" })
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        await services.GetRequiredService<AdminProvisioner>().ProvisionAsync();
        await services.GetRequiredService<NctbCatalogSeeder>().SeedAsync();
        await services.GetRequiredService<NctbBookCatalogSeeder>().SeedAsync();
        if (app.Environment.IsDevelopment())
            await services.GetRequiredService<DevelopmentDataSeeder>().SeedAsync();

        var db = services.GetRequiredService<ApplicationDbContext>();
        if (!await db.SitePages.AnyAsync(page => page.Slug == "welcome-email-template"))
        {
            db.SitePages.Add(new SitePage
            {
                Slug = "welcome-email-template", Title = "Welcome to EduMation, {FirstName}!",
                MetaDescription = "Internal welcome-email template; managed by administrators.",
                Body = "Hi {FirstName},\n\nWelcome to EduMation! Your account is ready, and we’re excited to help you learn.\n\nExplore your class curriculum, save lessons, practice with questions, track your progress, and add important dates to your calendar.\n\nYour EduMation user ID: {UserId}\nYour class: {Class}\n\nStart learning: {LoginUrl}\nYour calendar: {CalendarUrl}\nAI practice: {PracticeUrl}\n\nHappy learning!\nThe EduMation team",
                IsPublished = false, UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Application database initialization failed.");
        throw;
    }
}

app.Run();
