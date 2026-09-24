using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

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

        [HttpPost]
        public async Task<IActionResult> Add(int videoId, string returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized attempt to add favorite - no user ID.");
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Videos", new { id = videoId }) });
            }

            _logger.LogInformation("Add to Favorites called for videoId: {VideoId}, userId: {UserId}", videoId, userId);

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoId == videoId);

            if (favorite == null)
            {
                favorite = new Favorites { UserId = userId, VideoId = videoId, AddedDate = DateTime.Now };
                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added video {VideoId} to favorites for user {UserId}.", videoId, userId);
            }
            else
            {
                _logger.LogInformation("Video {VideoId} already in favorites for user {UserId}.", videoId, userId);
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
                var favorites = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Video)
                    .Select(f => f.Video)
                    .ToListAsync();

                favorites = favorites.Where(v => v != null).ToList();
                _logger.LogInformation("Retrieved {Count} favorite videos for user {UserId}.", favorites.Count, userId);
                return View(favorites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving favorites for user {UserId}.", userId);
                return StatusCode(500, "An error occurred while retrieving your favorites.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int videoId, string returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Unauthorized attempt to remove favorite - no user ID.");
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Videos", new { id = videoId }) });
            }

            _logger.LogInformation("Remove from Favorites called for videoId: {VideoId}, userId: {UserId}", videoId, userId);

            var favorite = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.VideoId == videoId);

            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed video {VideoId} from favorites for user {UserId}.", videoId, userId);
            }
            else
            {
                _logger.LogWarning("Favorite not found for video {VideoId} and user {UserId}.", videoId, userId);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("MyFavorites", "Favorites");
        }
    }
}