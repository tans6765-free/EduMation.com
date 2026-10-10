using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;

namespace EduMation.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ProfileController> _logger;
        private readonly IStudentEngagementService _engagement;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<ProfileController> logger, IStudentEngagementService engagement)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger;
            _engagement = engagement;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var profile = await _context.Profiles.Include(item => item.User).Include(item => item.LearningClass)
                .Include(item => item.AcademicYear).FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                profile = new Profile
                {
                    UserId = userId,
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    Email = email,
                    District = string.Empty,
                    ContactNumber = string.Empty,
                    Age = null,
                    Address = string.Empty,
                    ProfilePicturePath = "/images/default-profile.png"
                };
                _context.Profiles.Add(profile);
                await _context.SaveChangesAsync();
                await _context.Entry(profile).Reference(item => item.User).LoadAsync();
            }

            var isEmailVerified = profile.User?.EmailConfirmed ?? false;
            if (ProfileCompletion.IsComplete(profile, isEmailVerified))
                await _engagement.AwardAchievementAsync(userId, ProfileCompletion.AchievementCode, ProfileCompletion.AchievementTitle, "Your learning profile is ready. Great start!", "🌟");
            await PopulateDashboardAsync(profile, userId, isEmailVerified, Request.Query["edit"] == "true");
            return View(profile);
        }

        [HttpPost, RequestSizeLimit(6 * 1024 * 1024)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Profile model, IFormFile profilePicture)
        {
            // The posted profile is only an edit form payload. Validate the fields
            // the student can change instead of validating EF navigation properties.
            ModelState.Clear();
            model.FullName = model.FullName?.Trim() ?? string.Empty;
            model.District = model.District?.Trim() ?? string.Empty;
            model.ContactNumber = model.ContactNumber?.Trim() ?? string.Empty;
            model.Address = model.Address?.Trim() ?? string.Empty;
            model.LearningPreferences = model.LearningPreferences?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(model.FullName))
                ModelState.AddModelError(nameof(model.FullName), "Enter your name.");
            if (model.ContactNumber.Length > 40 || model.Address.Length > 300 || model.District.Length > 80 || model.FullName.Length > 120)
            {
                ModelState.AddModelError(string.Empty, "Check your contact number, district, full name, and address lengths.");
            }
            if (model.Age is < 8 or > 120)
                ModelState.AddModelError(nameof(model.Age), "Age must be between 8 and 120, or left blank.");
            if (model.PreferredLanguage is not ("en" or "bn") || model.LearningPreferences.Length > 1000)
                ModelState.AddModelError(string.Empty, "Choose a supported language and keep learning preferences under 1000 characters.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return Unauthorized();
            }

            var profile = await _context.Profiles.Include(item => item.User).Include(item => item.LearningClass)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                TempData["ErrorMessage"] = "Profile not found.";
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                profile.FullName = model.FullName;
                profile.District = model.District;
                profile.ContactNumber = model.ContactNumber;
                profile.Age = model.Age;
                profile.Address = model.Address;
                profile.LearningPreferences = model.LearningPreferences;
                profile.PreferredLanguage = model.PreferredLanguage;
                await PopulateDashboardAsync(profile, userId, profile.User?.EmailConfirmed ?? false, true);
                return View("Index", profile);
            }

            if (model.LearningClassId.HasValue)
            {
                var selectedClass = await _context.LearningClasses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == model.LearningClassId.Value && item.IsPublished);
                if (selectedClass == null)
                {
                    ModelState.AddModelError(nameof(model.LearningClassId), "Choose a published class.");
                    await PopulateDashboardAsync(profile, userId, profile.User?.EmailConfirmed ?? false, true);
                    return View("Index", profile);
                }
                profile.LearningClassId = selectedClass.Id;
                profile.AcademicYearId = selectedClass.AcademicYearId;
            }
            else
            {
                profile.LearningClassId = null;
                profile.AcademicYearId = null;
            }

            // Keep the attempted values in the form if image validation or saving fails.
            profile.FirstName = model.FirstName;
            profile.LastName = model.LastName;
            profile.District = model.District;
            profile.ContactNumber = model.ContactNumber;
            profile.Age = model.Age;
            profile.Address = model.Address;
            profile.PreferredLanguage = model.PreferredLanguage;
            profile.LearningPreferences = model.LearningPreferences;

            // Handle profile picture upload
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // Check file size limit
                if (profilePicture.Length > 5 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "The file is too large. Maximum size is 5MB.";
                    await PopulateDashboardAsync(profile, userId, profile.User?.EmailConfirmed ?? false, true);
                    return View("Index", profile);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images/profiles");
                var extension = Path.GetExtension(Path.GetFileName(profilePicture.FileName)).ToLowerInvariant();
                var expectedMime = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => string.Empty };
                if (string.IsNullOrEmpty(expectedMime) || profilePicture.ContentType != expectedMime || !await IsValidImageAsync(profilePicture, extension))
                {
                    TempData["ErrorMessage"] = "Choose a supported image with a valid file signature (maximum 5 MB).";
                    await PopulateDashboardAsync(profile, userId, profile.User?.EmailConfirmed ?? false, true);
                    return View("Index", profile);
                }
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(stream);
                    }
                    // Delete old profile picture if it exists and isn't the default
                    if (!string.IsNullOrEmpty(profile.ProfilePicturePath) && profile.ProfilePicturePath.StartsWith("/images/profiles/", StringComparison.Ordinal))
                    {
                        var oldFilePath = Path.Combine(uploadsFolder, Path.GetFileName(profile.ProfilePicturePath));
                        if (System.IO.File.Exists(oldFilePath))
                            System.IO.File.Delete(oldFilePath);
                    }

                    profile.ProfilePicturePath = $"/images/profiles/{fileName}";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Failed to upload profile picture. Please try again.";
                    _logger.LogError(ex, "Profile image upload failed for the current account.");
                    await PopulateDashboardAsync(profile, userId, profile.User?.EmailConfirmed ?? false, true);
                    return View("Index", profile);
                }
            }
            else
            {
            }

            try
            {
                await _context.SaveChangesAsync();
                var account = await _context.Users.AsNoTracking().Where(item => item.Id == userId).Select(item => item.EmailConfirmed).FirstOrDefaultAsync();
                if (ProfileCompletion.IsComplete(profile, account))
                    await _engagement.AwardAchievementAsync(userId, ProfileCompletion.AchievementCode, ProfileCompletion.AchievementTitle, "Your learning profile is ready. Great start!", "🌟");
                TempData["Message"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to save profile changes. Please try again.";
                _logger.LogError(ex, "Profile update failed for the current account.");
                await PopulateDashboardAsync(profile, userId, profile.User?.EmailConfirmed ?? false, true);
                return View("Index", profile);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLearningOptionsAsync()
        {
            ViewBag.LearningClasses = await _context.LearningClasses.AsNoTracking().Where(item => item.IsPublished).Include(item => item.AcademicYear).OrderBy(item => item.DisplayOrder).ToListAsync();
        }

        private async Task PopulateDashboardAsync(Profile profile, string userId, bool isEmailVerified, bool isEditing)
        {
            var completion = ProfileCompletion.Percentage(profile, isEmailVerified);
            ViewBag.ProfileCompletion = completion;
            ViewBag.EmailVerified = isEmailVerified;
            ViewBag.IsEditing = isEditing;
            ViewBag.NextStep = !isEmailVerified ? "Verify your email address" :
                string.IsNullOrWhiteSpace(profile.FullName) ? "Add your name" :
                !profile.LearningClassId.HasValue ? "Choose your class" :
                string.IsNullOrWhiteSpace(profile.District) ? "Add your district" : "Profile complete";
            ViewBag.NeedsEmailVerification = !isEmailVerified;

            var totalLessons = await _context.Lessons.CountAsync(item => item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished);
            var completedLessons = await _context.LessonProgresses.CountAsync(item => item.UserId == userId && item.IsCompleted);
            ViewBag.CompletedLessons = completedLessons;
            ViewBag.TotalLessons = totalLessons;
            ViewBag.ProgressPercent = totalLessons == 0 ? 0 : (int)Math.Round(completedLessons * 100d / totalLessons);
            ViewBag.Achievements = await _context.StudentAchievements.AsNoTracking().Where(item => item.UserId == userId).OrderByDescending(item => item.EarnedAtUtc).Take(8).ToListAsync();
            await PopulateLearningOptionsAsync();
        }

        private static async Task<bool> IsValidImageAsync(IFormFile file, string extension)
        {
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var count = await stream.ReadAsync(header.AsMemory());
            return extension switch
            {
                ".png" => count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".jpg" or ".jpeg" => count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
                ".gif" => count >= 6 && Encoding.ASCII.GetString(header, 0, 6) is "GIF87a" or "GIF89a",
                ".webp" => count >= 12 && Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
                _ => false
            };
        }
    }
}
