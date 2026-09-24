using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Diagnostics;

namespace EduMation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch the latest uploaded video as the featured video
            var featuredVideo = await _context.Videos
                .OrderByDescending(v => v.UploadDate)
                .FirstOrDefaultAsync();

            // Fetch all videos for the video list (excluding the featured video)
            var videos = await _context.Videos
                .Where(v => featuredVideo == null || v.Id != featuredVideo.Id)
                .ToListAsync();

            // Determine the most watched genre for the current user (if logged in)
            string mostWatchedGenre = null;
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var watchHistory = await _context.WatchHistories
                        .Where(wh => wh.UserId == userId)
                        .Include(wh => wh.Video)
                        .ToListAsync();

                    if (watchHistory.Any())
                    {
                        mostWatchedGenre = watchHistory
                            .GroupBy(wh => wh.Video.Genre)
                            .OrderByDescending(g => g.Count())
                            .Select(g => g.Key)
                            .FirstOrDefault();
                    }
                }
            }

            // Fetch recommended videos based on the most watched genre (excluding the featured video)
            List<Video> recommendedVideos = new List<Video>();
            if (!string.IsNullOrEmpty(mostWatchedGenre))
            {
                recommendedVideos = await _context.Videos
                    .Where(v => v.Genre == mostWatchedGenre && (featuredVideo == null || v.Id != featuredVideo.Id))
                    .Take(3) // Limit to 3 recommended videos
                    .ToListAsync();
            }
            else
            {
                // If no watch history or user not logged in, recommend the most recent videos
                recommendedVideos = await _context.Videos
                    .Where(v => featuredVideo == null || v.Id != featuredVideo.Id)
                    .OrderByDescending(v => v.UploadDate)
                    .Take(3)
                    .ToListAsync();
            }

            // Create a view model to pass all data to the view
            var viewModel = new HomeViewModel
            {
                FeaturedVideo = featuredVideo,
                RecommendedVideos = recommendedVideos,
                Videos = videos
            };

            return View(viewModel);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}