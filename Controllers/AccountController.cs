using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EduMation.Models;
using EduMation.Data;
using EduMation.ViewModels;
using System.Threading.Tasks;

namespace EduMation.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: Register
        [HttpGet]
        public IActionResult Register() => View();

        // POST: Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model == null)
            {
                ModelState.AddModelError(string.Empty, "Model data is invalid.");
                return View();
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    Email = model.Email,
                    EmailConfirmed = false // Require email verification
                };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Simulate sending verification code (1725)
                    Console.WriteLine($"Verification code for {model.Email}: 1725");
                    // For production, use SMTP (e.g., SendGrid):
                    // await SendEmailAsync(model.Email, "Verify Your Email", "Your verification code is 1725");

                    TempData["UserId"] = user.Id;
                    return RedirectToAction("VerifyEmail");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        // GET: VerifyEmail
        [HttpGet]
        public IActionResult VerifyEmail() => View();

        // POST: VerifyEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Code))
            {
                ModelState.AddModelError(string.Empty, "Verification code is required.");
                return View();
            }

            if (model.Code == "1725") // Static code for localhost
            {
                var userId = TempData["UserId"]?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    ModelState.AddModelError(string.Empty, "Session expired. Please register again.");
                    return View();
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                    return View();
                }

                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);

                var subscription = new Subscription
                {
                    UserId = user.Id,
                    Plan = "Basic",
                    Price = 0.00m,
                    User = user,
                    StartDate = DateTime.Now,
                    IsActive = true,
                    MaxVideos = 5, // Changed from VideoLimit to MaxVideos
                    TotalWatched = 0
                };

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid verification code.");
            return View();
        }

        // GET: Login
        [HttpGet]
        public IActionResult Login() => View();

        // POST: Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (model == null)
            {
                ModelState.AddModelError(string.Empty, "Model data is invalid.");
                return View();
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Username);
                if (user != null && !user.EmailConfirmed)
                {
                    ModelState.AddModelError(string.Empty, "Please verify your email before logging in.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
            }

            return View(model);
        }

        // GET: ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        // POST: ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Email))
            {
                ModelState.AddModelError(string.Empty, "Email is required.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No account found with this email.");
                return View();
            }

            // Simulate sending reset code (1725)
            Console.WriteLine($"Password reset code for {model.Email}: 1725");
            // For production, use SMTP:
            // await SendEmailAsync(model.Email, "Password Reset", "Your reset code is 1725");

            TempData["Email"] = model.Email;
            return RedirectToAction("ResetPassword");
        }

        // GET: ResetPassword
        [HttpGet]
        public IActionResult ResetPassword() => View();

        // POST: ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Code) || string.IsNullOrEmpty(model.NewPassword))
            {
                ModelState.AddModelError(string.Empty, "All fields are required.");
                return View();
            }

            var email = TempData["Email"]?.ToString();
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "Session expired. Please try again.");
                return View();
            }

            if (model.Code == "1725") // Static code for localhost
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "User not found.");
                    return View();
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (result.Succeeded)
                {
                    return RedirectToAction("Login");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid reset code.");
            }

            return View();
        }

        // POST: Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}