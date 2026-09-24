using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Security.Claims;

namespace EduMation.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string query)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var favoritedVideoIds = userId != null
                ? await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.VideoId)
                    .ToListAsync()
                : new List<int>();

            ViewBag.FavoritedVideoIds = favoritedVideoIds;

            var videos = string.IsNullOrEmpty(query)
                ? await _context.Videos.ToListAsync()
                : await _context.Videos
                    .Where(v => EF.Functions.Like(v.Title, $"%{query}%") || EF.Functions.Like(v.Genre, $"%{query}%"))
                    .ToListAsync();

            return View(videos);
        }
    }
}