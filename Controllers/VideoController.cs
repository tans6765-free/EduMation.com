using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Security.Claims;

namespace EduMation.Controllers
{
    public class VideosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VideosController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var favoritedVideoIds = userId != null
                ? await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.VideoId)
                    .ToListAsync()
                : new List<int>();

            ViewBag.FavoritedVideoIds = favoritedVideoIds;

            var videosQuery = _context.Videos.AsQueryable();
            if (!User.IsInRole("Admin")) videosQuery = videosQuery.Where(video => video.IsPublished);
            var videos = await videosQuery.OrderByDescending(video => video.UploadDate).ToListAsync();
            return View(videos);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var favoritedVideoIds = userId != null
                ? await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.VideoId)
                    .ToListAsync()
                : new List<int>();

            ViewBag.FavoritedVideoIds = favoritedVideoIds;

            var video = await _context.Videos.FirstOrDefaultAsync(v => v.Id == id && (v.IsPublished || User.IsInRole("Admin")));
            if (video == null)
            {
                return NotFound();
            }

            return View(video);
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaybackProgress(int id, double position, double duration, CancellationToken cancellationToken)
        {
            if (duration <= 0 || position < 0 || !double.IsFinite(position) || !double.IsFinite(duration)) return BadRequest();
            var exists = await _context.Videos.AnyAsync(video => video.Id == id && video.IsPublished, cancellationToken);
            if (!exists) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTime.UtcNow;
            var history = await _context.WatchHistories.FirstOrDefaultAsync(item => item.UserId == userId && item.VideoId == id, cancellationToken);
            var isMeaningfulStart = position >= 5;
            if (history == null)
            {
                history = new WatchHistory { UserId = userId, VideoId = id, WatchDate = now, WatchCount = isMeaningfulStart ? 1 : 0 };
                _context.WatchHistories.Add(history);
            }
            else if (history.StartedAtUtc == null && isMeaningfulStart)
            {
                history.WatchCount++;
            }

            if (isMeaningfulStart) history.StartedAtUtc ??= now;
            history.WatchDate = now;
            history.LastPositionSeconds = Math.Min(position, duration);
            history.ProgressPercent = Math.Max(history.ProgressPercent, Math.Clamp((int)Math.Round(position / duration * 100), 0, 100));
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { history.ProgressPercent, history.LastPositionSeconds });
        }
    }
}
