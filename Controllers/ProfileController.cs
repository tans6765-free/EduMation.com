using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace EduMation.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? "noemail@edumation.com";
                profile = new Profile
                {
                    UserId = userId,
                    FirstName = "Default",
                    LastName = "User",
                    Email = email,
                    ContactNumber = "N/A",
                    Age = null,
                    Address = "N/A",
                    ProfilePicturePath = "/images/default-profile.png"
                };
                _context.Profiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Profile model, IFormFile profilePicture)
        {
            // Clear ModelState and re-validate only the intended fields
            ModelState.Clear();
            TryValidateModel(model);

            // Exclude navigation property and file-related validation
            ModelState.Remove("User"); // Remove validation for the User navigation property
            ModelState.Remove("ProfilePicturePath"); // Remove validation for ProfilePicturePath if not required
            // Note: profilePicture is an IFormFile parameter, not part of ModelState, so no need to remove

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Model validation failed. Check your input.";
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                foreach (var error in errors)
                {
                    Console.WriteLine($"Validation Error: {error}");
                }
                return View("Index", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || model.UserId != userId)
            {
                TempData["ErrorMessage"] = "Unauthorized access.";
                return Unauthorized();
            }

            var profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                TempData["ErrorMessage"] = "Profile not found.";
                return NotFound();
            }

            // Handle profile picture upload
            if (profilePicture != null && profilePicture.Length > 0)
            {
                // Check file size limit
                if (profilePicture.Length > 5 * 1024 * 1024) // 5MB limit
                {
                    TempData["ErrorMessage"] = "The file is too large. Maximum size is 5MB.";
                    return View("Index", model);
                }

                Console.WriteLine($"Received file: {profilePicture.FileName}, Size: {profilePicture.Length} bytes");

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images/profiles");
                if (!Directory.Exists(uploadsFolder))
                {
                    Console.WriteLine($"Creating directory: {uploadsFolder}");
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{userId}-{DateTime.Now.Ticks}{Path.GetExtension(profilePicture.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePicture.CopyToAsync(stream);
                    }
                    Console.WriteLine($"File saved to: {filePath}");

                    // Delete old profile picture if it exists and isn't the default
                    if (!string.IsNullOrEmpty(profile.ProfilePicturePath) && profile.ProfilePicturePath != "/images/default-profile.png")
                    {
                        var oldFilePath = Path.Combine(_environment.WebRootPath, profile.ProfilePicturePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                            Console.WriteLine($"Deleted old file: {oldFilePath}");
                        }
                    }

                    profile.ProfilePicturePath = $"/images/profiles/{fileName}";
                    Console.WriteLine($"Updated ProfilePicturePath to: {profile.ProfilePicturePath}");
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Failed to upload profile picture. Please try again.";
                    Console.WriteLine($"Upload error: {ex.Message}");
                    return View("Index", model);
                }
            }
            else
            {
                Console.WriteLine("No profile picture provided.");
            }

            // Update other profile fields
            profile.FirstName = model.FirstName;
            profile.LastName = model.LastName;
            profile.ContactNumber = model.ContactNumber;
            profile.Age = model.Age;
            profile.Address = model.Address;

            try
            {
                _context.Update(profile);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to save profile changes. Please try again.";
                Console.WriteLine($"Save error: {ex.Message}");
                return View("Index", model);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}