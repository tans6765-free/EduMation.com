using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using EduMation.ViewModel;

namespace EduMation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMediaStorageService _mediaStorage;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, IMediaStorageService mediaStorage, ILogger<AdminController> logger)
        {
            _context = context;
            _mediaStorage = mediaStorage;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var studentIds = _context.UserRoles.Join(_context.Roles, userRole => userRole.RoleId, role => role.Id,
                (userRole, role) => new { userRole.UserId, role.Name }).Where(item => item.Name == "Student").Select(item => item.UserId);
            var recentCutoff = DateTime.UtcNow.AddDays(-7);
            var model = new AdminDashboardViewModel
            {
                TotalStudents = await studentIds.Distinct().CountAsync(),
                ActiveStudents = await _context.LessonProgresses.Where(progress => studentIds.Contains(progress.UserId) && progress.LastViewedAt >= recentCutoff).Select(progress => progress.UserId).Distinct().CountAsync(),
                PublishedLessons = await _context.Lessons.CountAsync(lesson => lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished),
                CompletedLessons = await _context.LessonProgresses.CountAsync(progress => progress.IsCompleted),
                PracticeAttempts = await _context.QuestionAttempts.CountAsync(),
                AveragePracticeScore = await _context.QuestionAttempts.Where(attempt => attempt.MaximumScore > 0).Select(attempt => (decimal?)(attempt.Score * 100 / attempt.MaximumScore)).AverageAsync() ?? 0,
                PublishedVideos = await _context.Videos.CountAsync(video => video.IsPublished)
            };
            var aiSince = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var aiLogs = _context.AiUsageLogs.Where(log => log.CreatedAtUtc >= aiSince);
            model.AiCallsThisMonth = await aiLogs.CountAsync();
            model.AiFailuresThisMonth = await aiLogs.CountAsync(log => log.Outcome == "Failure");
            model.AverageAiLatencyMs = await aiLogs.Select(log => (double?)log.DurationMs).AverageAsync() ?? 0;
            model.AiCallsByFeature = await aiLogs.GroupBy(log => log.Feature).Select(group => new AiUsageMetricViewModel { Name = group.Key, Calls = group.Count(), Failures = group.Count(log => log.Outcome == "Failure"), AverageMs = group.Average(log => log.DurationMs) }).OrderByDescending(item => item.Calls).ToListAsync();
            model.AiCallsByProvider = await aiLogs.GroupBy(log => log.Provider).Select(group => new AiUsageMetricViewModel { Name = group.Key, Calls = group.Count(), Failures = group.Count(log => log.Outcome == "Failure"), AverageMs = group.Average(log => log.DurationMs) }).OrderByDescending(item => item.Calls).ToListAsync();
            return View(model);
        }

        public IActionResult Create()
        {
            return View(new VideoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VideoViewModel model, IFormFile? videoFile, IFormFile? thumbnailFile)
        {
            _logger.LogInformation("Create action called at {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            _logger.LogInformation("Model: Title={Title}, Genre={Genre}, Description={Description}", model.Title, model.Genre, model.Description);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                _logger.LogWarning("ModelState Errors: {Errors}", string.Join("; ", errors));
                TempData["ErrorMessage"] = "Validation failed. Please check your inputs: " + string.Join("; ", errors);
                return View(model);
            }

            if (videoFile == null || videoFile.Length == 0)
            {
                ModelState.AddModelError("videoFile", "Video file is required.");
                return View(model);
            }

            string videoUrl;
            string thumbnailUrl = "/Uploads/thumbnails/default.jpg";
            try
            {
                videoUrl = await _mediaStorage.SaveAsync(videoFile, MediaAssetKind.Video);
                if (thumbnailFile is { Length: > 0 })
                    thumbnailUrl = await _mediaStorage.SaveAsync(thumbnailFile, MediaAssetKind.Image);
            }
            catch (InvalidDataException)
            {
                ModelState.AddModelError("videoFile", "Use a supported video or image file with a valid file signature. Video size limit: 100 MB; thumbnail: 10 MB.");
                return View(model);
            }

            var video = new Video
            {
                Title = model.Title,
                Genre = model.Genre,
                Description = model.Description,
                VideoUrl = videoUrl,
                ThumbnailUrl = thumbnailUrl,
                UploadDate = DateTime.UtcNow,
                IsPublished = true
            };

            try
            {
                _context.Videos.Add(video);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Video uploaded successfully.";
                _logger.LogInformation("Video uploaded and saved to database successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving the video to the database.");
                TempData["ErrorMessage"] = "The video could not be saved. Please try again or contact support.";
                return View(model);
            }

            return RedirectToAction("Index", "Admin");
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var video = await _context.Videos.FindAsync(id);
            if (video == null)
            {
                return NotFound();
            }
            var model = new VideoViewModel
            {
                Id = video.Id,
                Title = video.Title,
                Genre = video.Genre,
                Description = video.Description,
                ThumbnailUrl = video.ThumbnailUrl,
                VideoUrl = video.VideoUrl,
                IsPublished = video.IsPublished
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VideoViewModel model, IFormFile? videoFile, IFormFile? thumbnailFile)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var video = await _context.Videos.FindAsync(id);
            if (video == null)
            {
                return NotFound();
            }

            if (videoFile != null && videoFile.Length > 0)
            {
                try { video.VideoUrl = await _mediaStorage.SaveAsync(videoFile, MediaAssetKind.Video); }
                catch (InvalidDataException) { ModelState.AddModelError("videoFile", "Choose a supported video file with a valid signature (maximum 100 MB)."); return View(model); }
            }

            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                try { video.ThumbnailUrl = await _mediaStorage.SaveAsync(thumbnailFile, MediaAssetKind.Image); }
                catch (InvalidDataException) { ModelState.AddModelError("thumbnailFile", "Choose a supported image file with a valid signature (maximum 10 MB)."); return View(model); }
            }

            video.Title = model.Title;
            video.Genre = model.Genre;
            video.Description = model.Description;
            video.IsPublished = model.IsPublished;

            try
            {
                _context.Update(video);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Video updated successfully.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VideoExists(video.Id))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var video = await _context.Videos
                .FirstOrDefaultAsync(m => m.Id == id);
            if (video == null)
            {
                return NotFound();
            }
            return View(video);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var video = await _context.Videos.FindAsync(id);
            if (video != null)
            {
                _context.Videos.Remove(video);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Video deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool VideoExists(int id)
        {
            return _context.Videos.Any(e => e.Id == id);
        }
    }
}
