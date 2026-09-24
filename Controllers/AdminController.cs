using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;

namespace EduMation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<AdminController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Videos.ToListAsync());
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
            _logger.LogInformation("VideoFile: Name={Name}, Length={Length}, ContentType={ContentType}", videoFile?.FileName, videoFile?.Length, videoFile?.ContentType);
            _logger.LogInformation("ThumbnailFile: Name={Name}, Length={Length}, ContentType={ContentType}", thumbnailFile?.FileName, thumbnailFile?.Length, thumbnailFile?.ContentType);

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
                TempData["ErrorMessage"] = "Video file is required.";
                return View(model);
            }

            if (videoFile.Length > 100 * 1024 * 1024) // 100 MB limit
            {
                ModelState.AddModelError("videoFile", "Video file size cannot exceed 100 MB.");
                TempData["ErrorMessage"] = "Video file size cannot exceed 100 MB.";
                return View(model);
            }

            if (!videoFile.ContentType.StartsWith("video/"))
            {
                ModelState.AddModelError("videoFile", "Invalid video file format.");
                TempData["ErrorMessage"] = "Invalid video file format.";
                return View(model);
            }

            var videoFileName = Guid.NewGuid().ToString() + Path.GetExtension(videoFile.FileName);
            var videoPath = Path.Combine(_environment.WebRootPath, "Uploads", "videos", videoFileName);
            var videoDir = Path.GetDirectoryName(videoPath);
            if (!string.IsNullOrEmpty(videoDir) && !Directory.Exists(videoDir))
            {
                Directory.CreateDirectory(videoDir);
            }
            try
            {
                using (var stream = new FileStream(videoPath, FileMode.Create, FileAccess.Write))
                {
                    await videoFile.CopyToAsync(stream);
                }
                _logger.LogInformation("Video saved to: {Path}", videoPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload video file. Check file permissions for {Path} and server configuration.", videoPath);
                TempData["ErrorMessage"] = $"Failed to upload video file: {ex.Message}. Check file permissions for {videoPath} and server configuration.";
                return View(model);
            }

            string? thumbnailFileName = null;
            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                if (thumbnailFile.Length > 10 * 1024 * 1024) // 10 MB limit
                {
                    ModelState.AddModelError("thumbnailFile", "Thumbnail file size cannot exceed 10 MB.");
                    TempData["ErrorMessage"] = "Thumbnail file size cannot exceed 10 MB.";
                    return View(model);
                }
                if (!thumbnailFile.ContentType.StartsWith("image/"))
                {
                    ModelState.AddModelError("thumbnailFile", "Invalid thumbnail file format.");
                    TempData["ErrorMessage"] = "Invalid thumbnail file format.";
                    return View(model);
                }
                thumbnailFileName = Guid.NewGuid().ToString() + Path.GetExtension(thumbnailFile.FileName);
                var thumbnailPath = Path.Combine(_environment.WebRootPath, "Uploads", "thumbnails", thumbnailFileName);
                var thumbnailDir = Path.GetDirectoryName(thumbnailPath);
                if (!string.IsNullOrEmpty(thumbnailDir) && !Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }
                try
                {
                    using (var stream = new FileStream(thumbnailPath, FileMode.Create, FileAccess.Write))
                    {
                        await thumbnailFile.CopyToAsync(stream);
                    }
                    _logger.LogInformation("Thumbnail saved to: {Path}", thumbnailPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload thumbnail file. Check file permissions for {Path} and server configuration.", thumbnailPath);
                    TempData["ErrorMessage"] = $"Failed to upload thumbnail file: {ex.Message}. Check file permissions for {thumbnailPath} and server configuration.";
                    return View(model);
                }
            }

            var video = new Video
            {
                Title = model.Title,
                Genre = model.Genre,
                Description = model.Description,
                VideoUrl = $"/Uploads/videos/{videoFileName}",
                ThumbnailUrl = thumbnailFileName != null ? $"/Uploads/thumbnails/{thumbnailFileName}" : "/Uploads/thumbnails/default.jpg",
                UploadDate = DateTime.Now
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
                TempData["ErrorMessage"] = $"An error occurred while saving the video to the database: {ex.Message}. Check database connectivity and schema.";
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
                VideoUrl = video.VideoUrl
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

            string? videoFileName = null;
            if (videoFile != null && videoFile.Length > 0)
            {
                if (videoFile.Length > 100 * 1024 * 1024)
                {
                    ModelState.AddModelError("videoFile", "Video file size cannot exceed 100 MB.");
                    return View(model);
                }
                if (!videoFile.ContentType.StartsWith("video/"))
                {
                    ModelState.AddModelError("videoFile", "Invalid video file format.");
                    return View(model);
                }
                videoFileName = Guid.NewGuid().ToString() + Path.GetExtension(videoFile.FileName);
                var videoPath = Path.Combine(_environment.WebRootPath, "Uploads", "videos", videoFileName);
                var videoDir = Path.GetDirectoryName(videoPath);
                if (!string.IsNullOrEmpty(videoDir))
                {
                    Directory.CreateDirectory(videoDir);
                }
                using (var stream = new FileStream(videoPath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(stream);
                }
                video.VideoUrl = $"/Uploads/videos/{videoFileName}";
            }

            string? thumbnailFileName = null;
            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                if (thumbnailFile.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("thumbnailFile", "Thumbnail file size cannot exceed 10 MB.");
                    return View(model);
                }
                if (!thumbnailFile.ContentType.StartsWith("image/"))
                {
                    ModelState.AddModelError("thumbnailFile", "Invalid thumbnail file format.");
                    return View(model);
                }
                thumbnailFileName = Guid.NewGuid().ToString() + Path.GetExtension(thumbnailFile.FileName);
                var thumbnailPath = Path.Combine(_environment.WebRootPath, "Uploads", "thumbnails", thumbnailFileName);
                var thumbnailDir = Path.GetDirectoryName(thumbnailPath);
                if (!string.IsNullOrEmpty(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }
                using (var stream = new FileStream(thumbnailPath, FileMode.Create))
                {
                    await thumbnailFile.CopyToAsync(stream);
                }
                video.ThumbnailUrl = $"/Uploads/thumbnails/{thumbnailFileName}";
            }

            video.Title = model.Title;
            video.Genre = model.Genre;
            video.Description = model.Description;

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