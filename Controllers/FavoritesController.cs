using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using EduMation.ViewModel;

namespace EduMation.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(ApplicationDbContext context, ILogger<FavoritesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int videoId, string returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized attempt to add favorite - no user ID.");
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Videos", new { id = videoId }) });
            }

            if (!await _context.Videos.AnyAsync(video => video.Id == videoId && video.IsPublished)) return NotFound();

            _logger.LogInformation("Adding a video to the current learner's saved list.");

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoId == videoId);

            if (favorite == null)
            {
                favorite = new Favorites { UserId = userId, VideoId = videoId, AddedDate = DateTime.Now };
                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved a video for the current learner.");
            }
            else
            {
                _logger.LogInformation("Video was already saved by the current learner.");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("MyFavorites", "Favorites");
        }

        public async Task<IActionResult> MyFavorites()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized attempt to view favorites - no user ID.");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var videos = await _context.Favorites
                    .Where(f => f.UserId == userId && f.Video.IsPublished)
                    .Include(f => f.Video)
                    .Select(f => f.Video)
                    .ToListAsync();

                var lessons = await _context.SavedLessons.Where(item => item.UserId == userId && item.Lesson.IsPublished && item.Lesson.Topic.IsPublished && item.Lesson.Topic.Chapter.IsPublished && item.Lesson.Topic.Chapter.Subject.IsPublished && item.Lesson.Topic.Chapter.Subject.LearningClass.IsPublished)
                    .Include(item => item.Lesson).ThenInclude(item => item.Topic).ThenInclude(item => item.Chapter).ThenInclude(item => item.Subject).ThenInclude(item => item.LearningClass)
                    .OrderByDescending(item => item.AddedAtUtc).ToListAsync();
                var savedVideos = videos.Where(video => video != null).Cast<Video>().ToList();
                _logger.LogInformation("Retrieved saved learning items for the current learner.");
                return View(new SavedItemsViewModel { Videos = savedVideos, Lessons = lessons });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving the current learner's saved list.");
                return StatusCode(500, "An error occurred while retrieving your favorites.");
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLesson(int lessonId, string? returnUrl)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var lessonExists = await _context.Lessons.AnyAsync(item => item.Id == lessonId && item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished);
            if (!lessonExists) return NotFound();
            if (!await _context.SavedLessons.AnyAsync(item => item.UserId == userId && item.LessonId == lessonId))
            {
                _context.SavedLessons.Add(new SavedLesson { UserId = userId, LessonId = lessonId, AddedAtUtc = DateTime.UtcNow });
                await _context.SaveChangesAsync();
            }
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(MyFavorites));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLesson(int lessonId, string? returnUrl)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var saved = await _context.SavedLessons.FirstOrDefaultAsync(item => item.UserId == userId && item.LessonId == lessonId);
            if (saved != null)
            {
                _context.SavedLessons.Remove(saved);
                await _context.SaveChangesAsync();
            }
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(MyFavorites));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int videoId, string returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized attempt to remove favorite - no user ID.");
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Videos", new { id = videoId }) });
            }

            _logger.LogInformation("Removing a video from the current learner's saved list.");

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoId == videoId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed a video from the current learner's saved list.");
            }
            else
            {
                _logger.LogWarning("Requested saved video was not found for the current learner.");
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("MyFavorites", "Favorites");
        }
    }
}
