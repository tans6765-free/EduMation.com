using EduMation.Models;
using EduMation.Services;
using EduMation.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using EduMation.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EduMation.Controllers;

public sealed class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> _signInManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _db;
    private readonly IDataProtector _verificationCodeProtector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStudentEngagementService _engagement;

    public AccountController(UserManager<ApplicationUser> userManager, Microsoft.AspNetCore.Identity.SignInManager<ApplicationUser> signInManager,
        IEmailSender emailSender, ILogger<AccountController> logger, IConfiguration configuration, IWebHostEnvironment environment, ApplicationDbContext db,
        IDataProtectionProvider protection, IHttpClientFactory httpClientFactory, IStudentEngagementService engagement)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _db = db;
        _verificationCodeProtector = protection.CreateProtector("EduMation.EmailVerificationCode.v1");
        _httpClientFactory = httpClientFactory;
        _engagement = engagement;
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        await PopulateClassesAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive-email")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) { await PopulateClassesAsync(); return View(model); }
        var learningClass = await _db.LearningClasses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == model.LearningClassId && item.IsPublished);
        if (learningClass == null)
        {
            ModelState.AddModelError(nameof(model.LearningClassId), "Choose an available class.");
            await PopulateClassesAsync();
            return View(model);
        }
        var user = new ApplicationUser { UserName = model.UserId.Trim(), Email = model.Email.Trim(), EmailConfirmed = false, RegisteredAtUtc = DateTimeOffset.UtcNow };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await PopulateClassesAsync();
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Student");
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Could not assign the learner role to a newly registered account.");
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "We could not finish creating your account. Please try again later.");
            await PopulateClassesAsync();
            return View(model);
        }

        var nameParts = SplitName(model.FullName);
        _db.Profiles.Add(new Profile
        {
            UserId = user.Id, FirstName = nameParts.First, LastName = nameParts.Last, Email = model.Email.Trim(),
            District = model.District.Trim(), ContactNumber = model.ContactNumber?.Trim() ?? string.Empty,
            LearningClassId = learningClass.Id, AcademicYearId = learningClass.AcademicYearId
        });
        await _db.SaveChangesAsync();

        try
        {
            await SendVerificationCodeAsync(user);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Verification email delivery failed for a newly registered account.");
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "We could not send the verification email. Please try again later.");
            await PopulateClassesAsync();
            return View(model);
        }

        TempData["Message"] = "Enter the six-digit verification code sent to your email.";
        return RedirectToAction(nameof(VerifyEmail), new { userId = user.Id });
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return BadRequest();
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();
        if (user.EmailConfirmed) return RedirectToAction(nameof(Login));
        return View(new VerifyEmailViewModel { UserId = userId, Email = user.Email ?? string.Empty });
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null) return BadRequest("The verification request is invalid.");
        if (user.EmailConfirmed) return RedirectToAction(nameof(Login));
        var now = DateTimeOffset.UtcNow;
        var codeMatches = false;
        if (user.EmailVerificationAttempts < 5 && user.EmailVerificationExpiresAtUtc > now && !string.IsNullOrWhiteSpace(user.ProtectedEmailVerificationCode))
        {
            user.EmailVerificationAttempts++;
            try
            {
                var expected = _verificationCodeProtector.Unprotect(user.ProtectedEmailVerificationCode);
                codeMatches = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(model.Code));
            }
            catch (CryptographicException) { }
        }
        if (!codeMatches)
        {
            await _userManager.UpdateAsync(user);
            ModelState.AddModelError(nameof(model.Code), "That code is incorrect or expired. Request a new code and try again.");
            return View(model);
        }

        user.EmailConfirmed = true;
        user.ProtectedEmailVerificationCode = null;
        user.EmailVerificationExpiresAtUtc = null;
        user.EmailVerificationAttempts = 0;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return BadRequest("Email verification could not be completed. Request a new code.");
        var profile = await _db.Profiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (profile != null && ProfileCompletion.IsComplete(profile, true))
            await _engagement.AwardAchievementAsync(user.Id, "profile-complete", "Profile complete", "Your learning profile is ready. Great start!", "🌟", cancellationToken);
        await SendWelcomeEmailAsync(user, cancellationToken);
        var externalLogins = await _userManager.GetLoginsAsync(user);
        if (externalLogins.Any(item => item.LoginProvider == "Google"))
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Message"] = "Your email is verified. You are signed in with Google.";
            return RedirectToAction("Index", "Profile");
        }
        TempData["Message"] = "Your email is verified. You can sign in now.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return RedirectToAction("Index", "Home");
        if (result.IsNotAllowed) ModelState.AddModelError(string.Empty, "Please verify your email before logging in.");
        else if (result.IsLockedOut) ModelState.AddModelError(string.Empty, "Too many attempts. Please try again later.");
        else ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return View(model);
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive-email")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is { EmailConfirmed: true })
        {
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
                var link = BuildEmailLink(nameof(ResetPassword), new { userId = user.Id, code });
                await _emailSender.SendAsync(user.Email!, "Reset your EduMation password", $"Open this one-time link to reset your password: {link}");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Password reset email delivery failed.");
            }
        }
        TempData["Message"] = "If an account matches that email, a password reset link has been sent.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("sensitive-email")]
    public async Task<IActionResult> ResendVerification(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Login));
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is { EmailConfirmed: false })
        {
            try
            {
                await SendVerificationCodeAsync(user);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Verification email resend failed.");
            }
        }
        TempData["Message"] = "If that account needs verification, a new code has been sent.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult GoogleLogin()
    {
        var clientId = _configuration["GoogleAuth:ClientId"] ?? _configuration["GoogleCalendar:ClientId"];
        var clientSecret = _configuration["GoogleAuth:ClientSecret"] ?? _configuration["GoogleCalendar:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            TempData["Message"] = "Google sign-in needs OAuth credentials. You can still use User ID and password.";
            return RedirectToAction(nameof(Login));
        }
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Response.Cookies.Append("edumation_google_signin_state", state, new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax, IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(10), Path = "/Account/GoogleCallback"
        });
        var redirectUri = GetGoogleSignInRedirectUri();
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["redirect_uri"] = redirectUri, ["response_type"] = "code",
            ["scope"] = "openid email profile", ["state"] = state, ["prompt"] = "select_account"
        };
        return Redirect("https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        var expectedState = Request.Cookies["edumation_google_signin_state"];
        Response.Cookies.Delete("edumation_google_signin_state", new CookieOptions { Path = "/Account/GoogleCallback", Secure = Request.IsHttps, SameSite = SameSiteMode.Lax });
        if (string.IsNullOrWhiteSpace(expectedState) || string.IsNullOrWhiteSpace(state) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedState), Encoding.UTF8.GetBytes(state)))
            return BadRequest("Google sign-in state is invalid. Please try again.");
        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code))
        {
            TempData["Message"] = "Google sign-in was cancelled.";
            return RedirectToAction(nameof(Login));
        }

        var clientId = _configuration["GoogleAuth:ClientId"] ?? _configuration["GoogleCalendar:ClientId"] ?? string.Empty;
        var clientSecret = _configuration["GoogleAuth:ClientSecret"] ?? _configuration["GoogleCalendar:ClientSecret"] ?? string.Empty;
        var redirectUri = GetGoogleSignInRedirectUri();
        try
        {
            using var http = _httpClientFactory.CreateClient();
            using var tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code, ["client_id"] = clientId, ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri, ["grant_type"] = "authorization_code"
            }), cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google sign-in token exchange failed with status {StatusCode}.", tokenResponse.StatusCode);
                TempData["Message"] = "Google sign-in could not be completed. Try again or use your User ID and password.";
                return RedirectToAction(nameof(Login));
            }
            using var tokenPayload = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
            var accessToken = tokenPayload.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(accessToken)) return RedirectToAction(nameof(Login));
            using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
            userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userInfoResponse = await http.SendAsync(userInfoRequest, cancellationToken);
            if (!userInfoResponse.IsSuccessStatusCode) return RedirectToAction(nameof(Login));
            using var userInfo = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync(cancellationToken));
            var root = userInfo.RootElement;
            var subject = root.GetProperty("sub").GetString();
            var email = root.GetProperty("email").GetString()?.Trim();
            var verified = root.TryGetProperty("email_verified", out var verifiedValue) && verifiedValue.GetBoolean();
            var fullName = root.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email) || !verified)
            {
                TempData["Message"] = "Google did not return a verified email address. Use email and password to register.";
                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByLoginAsync("Google", subject);
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = await CreateGoogleUserNameAsync(email), Email = email, EmailConfirmed = false,
                        RegisteredAtUtc = DateTimeOffset.UtcNow
                    };
                    var createResult = await _userManager.CreateAsync(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) + "aA1!");
                    if (!createResult.Succeeded)
                    {
                        _logger.LogWarning("Could not create a student account after Google sign-in.");
                        TempData["Message"] = "Your EduMation account could not be created. Please try again.";
                        return RedirectToAction(nameof(Login));
                    }
                    await _userManager.AddToRoleAsync(user, "Student");
                    var parts = SplitName(fullName);
                    _db.Profiles.Add(new Profile
                    {
                        UserId = user.Id, FirstName = parts.First, LastName = parts.Last, Email = email, District = string.Empty
                    });
                    await _db.SaveChangesAsync(cancellationToken);
                }
                var loginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", subject, "Google"));
                if (!loginResult.Succeeded && await _userManager.FindByLoginAsync("Google", subject) == null)
                {
                    _logger.LogWarning("Could not link a Google identity to an EduMation account.");
                    TempData["Message"] = "Google could not be connected to this EduMation account.";
                    return RedirectToAction(nameof(Login));
                }
            }
            if (!user.EmailConfirmed)
            {
                try
                {
                    await SendVerificationCodeAsync(user);
                    TempData["Message"] = "We sent a six-digit verification code to your Google email address.";
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Google account verification email delivery failed.");
                    TempData["Message"] = "Your Google account is ready, but we could not send the verification code. Configure email delivery, then request a new code from this page.";
                }
                return RedirectToAction(nameof(VerifyEmail), new { userId = user.Id });
            }
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Message"] = "You are signed in with Google. Complete your profile to unlock your profile badge.";
            return RedirectToAction("Index", "Profile");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Google sign-in could not be completed.");
            TempData["Message"] = "Google sign-in could not be completed. Try again or use your User ID and password.";
            return RedirectToAction(nameof(Login));
        }
    }

    [HttpGet]
    public IActionResult ResetPassword(string userId, string code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code)) return BadRequest();
        return View(new ResetPasswordViewModel { UserId = userId, Code = code });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null) return BadRequest("This reset link is invalid or expired.");
        try
        {
            var token = System.Text.Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["Message"] = "Your password has been reset. Sign in with your new password.";
                return RedirectToAction(nameof(Login));
            }
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or expired.");
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    private async Task SendWelcomeEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _db.SitePages.AsNoTracking().FirstOrDefaultAsync(page => page.Slug == "welcome-email-template", cancellationToken);
            if (template == null || string.IsNullOrWhiteSpace(user.Email)) return;
            var profile = await _db.Profiles.AsNoTracking().Include(item => item.LearningClass)
                .FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            var fullName = profile == null ? user.UserName ?? user.Email : $"{profile.FirstName} {profile.LastName}".Trim();
            var siteUrl = (_configuration["App:PublicBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{FirstName}"] = profile?.FirstName ?? fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "learner",
                ["{FullName}"] = fullName,
                ["{UserId}"] = user.UserName ?? string.Empty,
                ["{Email}"] = user.Email,
                ["{Class}"] = profile?.LearningClass?.Name ?? "Complete your profile to choose your class",
                ["{LoginUrl}"] = $"{siteUrl}/Account/Login",
                ["{CalendarUrl}"] = $"{siteUrl}/calendar",
                ["{PracticeUrl}"] = $"{siteUrl}/practice",
                ["{WebsiteName}"] = "EduMation"
            };
            string Expand(string value)
            {
                foreach (var token in tokens) value = value.Replace(token.Key, token.Value, StringComparison.OrdinalIgnoreCase);
                return value;
            }
            await _emailSender.SendAsync(user.Email, Expand(template.Title), Expand(template.Body), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Welcome email delivery failed for a verified student account.");
        }
    }

    private string BuildEmailLink(string action, object routeValues)
    {
        var path = Url.Action(action, "Account", routeValues) ?? throw new InvalidOperationException("Could not build account email link.");
        var configuredBaseUrl = _configuration["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            if (!_environment.IsDevelopment()) throw new InvalidOperationException("App:PublicBaseUrl must be configured for account email links.");
            configuredBaseUrl = $"{Request.Scheme}://{Request.Host}";
        }

        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps && !_environment.IsDevelopment())
            throw new InvalidOperationException("App:PublicBaseUrl must be an absolute HTTPS URL in production.");
        var normalizedBaseUri = new Uri(baseUri.ToString().TrimEnd('/') + "/");
        return new Uri(normalizedBaseUri, path.TrimStart('/')).ToString();
    }

    private async Task PopulateClassesAsync() => ViewBag.LearningClasses = await _db.LearningClasses.AsNoTracking()
        .Where(item => item.IsPublished).OrderBy(item => item.DisplayOrder).ToListAsync();

    private async Task SendVerificationCodeAsync(ApplicationUser user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.ProtectedEmailVerificationCode = _verificationCodeProtector.Protect(code);
        user.EmailVerificationExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15);
        user.EmailVerificationAttempts = 0;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded) throw new InvalidOperationException("Could not save email verification state.");
        await _emailSender.SendAsync(user.Email!, "Your EduMation verification code", $"Your six-digit EduMation verification code is {code}. It expires in 15 minutes. If you did not create this account, ignore this email.");
    }

    private string GetGoogleSignInRedirectUri() => _configuration["GoogleAuth:RedirectUri"] ??
        $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Account/GoogleCallback";

    private async Task<string> CreateGoogleUserNameAsync(string email)
    {
        var localPart = email.Split('@')[0];
        var baseName = new string(localPart.Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-').ToArray());
        if (baseName.Length < 3) baseName = "learner";
        baseName = baseName[..Math.Min(50, baseName.Length)];
        var candidate = baseName;
        while (await _userManager.FindByNameAsync(candidate) != null)
        {
            var suffix = RandomNumberGenerator.GetInt32(1000, 9999).ToString();
            candidate = baseName[..Math.Min(baseName.Length, 50 - suffix.Length)] + suffix;
        }
        return candidate;
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (parts.FirstOrDefault() ?? "Student", parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty);
    }
}
