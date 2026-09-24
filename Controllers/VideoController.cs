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

            var videos = await _context.Videos.ToListAsync();
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

            var video = await _context.Videos.FirstOrDefaultAsync(v => v.Id == id);
            if (video == null)
            {
                return NotFound();
            }

            // Update Watch History if user is authenticated
            if (userId != null)
            {
                var watchHistory = await _context.WatchHistories
                    .FirstOrDefaultAsync(wh => wh.UserId == userId && wh.VideoId == id);

                if (watchHistory == null)
                {
                    // Add new watch history entry
                    watchHistory = new WatchHistory
                    {
                        UserId = userId,
                        VideoId = (int)id,
                        WatchDate = DateTime.Now,
                        WatchCount = 1
                    };
                    _context.WatchHistories.Add(watchHistory);
                }
                else
                {
                    // Update existing entry
                    watchHistory.WatchDate = DateTime.Now;
                    watchHistory.WatchCount += 1;
                }

                await _context.SaveChangesAsync();
            }

            return View(video);
        }
    }
}