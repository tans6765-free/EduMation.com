using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;

namespace EduMation.Controllers
{
    [Authorize]
    public class WatchHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WatchHistoryController> _logger;

        public WatchHistoryController(ApplicationDbContext context, ILogger<WatchHistoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found for watch history.");
                return Unauthorized();
            }

            var watchHistory = await _context.WatchHistories
                .Where(wh => wh.UserId == userId)
                .Include(wh => wh.Video)
                .OrderByDescending(wh => wh.WatchDate)
                .ToListAsync();

            return View(watchHistory);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User ID not found for clearing watch history.");
                return Unauthorized();
            }

            var watchHistory = await _context.WatchHistories
                .Where(wh => wh.UserId == userId)
                .ToListAsync();

            _context.WatchHistories.RemoveRange(watchHistory);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Watch history cleared for user {UserId}", userId);

            return RedirectToAction(nameof(Index));
        }
    }
}