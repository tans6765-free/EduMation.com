using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EduMation.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionController(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: /Subscription/Upgrade
        public IActionResult Upgrade()
        {
            var plans = new[]
            {
                new { Name = "Basic", Price = 5.00m, Duration = 30, MaxVideos = 100, Description = "Basic plan: $5.00, 30 days, 100 videos" },
                new { Name = "Advanced", Price = 10.00m, Duration = 30, MaxVideos = 500, Description = "Advanced plan: $10.00, 30 days, 500 videos" },
                new { Name = "Premium", Price = 20.00m, Duration = 30, MaxVideos = 1000, Description = "Premium plan: $20.00, 30 days, 1000 videos" }
            };
            ViewBag.Plans = plans;
            return View();
        }

        // POST: /Subscription/Upgrade
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upgrade(string plan)
        {
            if (plan != "Basic" && plan != "Advanced" && plan != "Premium")
            {
                TempData["ErrorMessage"] = "Invalid plan selected.";
                return RedirectToAction(nameof(Upgrade));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (subscription != null)
            {
                TempData["ErrorMessage"] = "You already have a subscription. Delete it first and then try again. Thank you";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Preserve WatchHistories for the current month, delete others
                var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var watchHistoriesToDelete = await _context.WatchHistories
                    .Where(wh => wh.UserId == userId && wh.WatchDate < firstDayOfMonth)
                    .ToListAsync();
                if (watchHistoriesToDelete.Any())
                {
                    _context.WatchHistories.RemoveRange(watchHistoriesToDelete);
                    await _context.SaveChangesAsync();
                }

                int maxVideos = plan == "Premium" ? 1000 : plan == "Advanced" ? 500 : 100;
                decimal price = plan == "Premium" ? 20.00m : plan == "Advanced" ? 10.00m : 5.00m;

                subscription = new Subscription
                {
                    UserId = userId,
                    Plan = plan,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(30),
                    Price = price,
                    MaxVideos = maxVideos,
                    IsActive = true,
                    TotalWatched = 0,
                    LastWatchedMonth = DateTime.Now.Month,
                    LastWatchedYear = DateTime.Now.Year
                };
                _context.Subscriptions.Add(subscription);

                await _context.SaveChangesAsync();
                TempData["Message"] = "Subscription upgraded successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while upgrading your subscription. Please try again.";
                Console.WriteLine($"Upgrade error: {ex.Message}");
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Subscription/Index
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (subscription == null)
            {
                return View("NoSubscription");
            }

            // Fallback: Recalculate TotalWatched when viewing the subscription page
            subscription.TotalWatched = await _context.WatchHistories
                .Where(wh => wh.UserId == userId && wh.WatchDate >= subscription.StartDate)
                .SumAsync(wh => wh.WatchCount);

            await _context.SaveChangesAsync();

            return View(subscription);
        }

        // GET: /Subscription/Create
        public IActionResult Create()
        {
            return View(new Subscription());
        }

        // POST: /Subscription/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Subscription model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingSubscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existingSubscription != null)
            {
                TempData["ErrorMessage"] = "You already have a subscription. Delete it first and then try again. Thank you";
                return RedirectToAction(nameof(Index));
            }

            if (model.EndDate <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "End date must be in the future.";
                return View(model);
            }

            if (model.Price <= 0)
            {
                TempData["ErrorMessage"] = "Price must be greater than zero.";
                return View(model);
            }

            try
            {
                // Preserve WatchHistories for the current month, delete others
                var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var watchHistoriesToDelete = await _context.WatchHistories
                    .Where(wh => wh.UserId == userId && wh.WatchDate < firstDayOfMonth)
                    .ToListAsync();
                if (watchHistoriesToDelete.Any())
                {
                    _context.WatchHistories.RemoveRange(watchHistoriesToDelete);
                    await _context.SaveChangesAsync();
                }

                var days = (model.EndDate - DateTime.Now).Days;
                var videoLimit = (int)Math.Ceiling((5.0 / 3.0) * days * (double)model.Price);

                var subscription = new Subscription
                {
                    UserId = userId,
                    Plan = "Custom",
                    StartDate = DateTime.Now,
                    EndDate = model.EndDate,
                    Price = model.Price,
                    MaxVideos = videoLimit,
                    IsActive = true,
                    TotalWatched = 0,
                    LastWatchedMonth = DateTime.Now.Month,
                    LastWatchedYear = DateTime.Now.Year
                };

                _context.Subscriptions.Add(subscription);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Subscription created successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while creating your subscription. Please try again.";
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Subscription/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (subscription == null)
            {
                TempData["ErrorMessage"] = "No subscription found to delete or unauthorized.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Clear all WatchHistories for the user to reset TotalWatched
                var watchHistories = await _context.WatchHistories
                    .Where(wh => wh.UserId == userId)
                    .ToListAsync();
                if (watchHistories.Any())
                {
                    _context.WatchHistories.RemoveRange(watchHistories);
                    await _context.SaveChangesAsync();
                }

                _context.Subscriptions.Remove(subscription);
                await _context.SaveChangesAsync();
                TempData["Message"] = "Subscription deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting your subscription. Please try again.";
                Console.WriteLine($"Delete error: {ex.Message}");
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Subscription/IncrementVideosWatched
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> IncrementVideosWatched()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (subscription == null)
            {
                Console.WriteLine("No active subscription found for user: " + userId);
                return Json(new { success = false, message = "No active subscription found." });
            }

            try
            {
                // Recalculate TotalWatched based on WatchHistory
                var totalWatched = await _context.WatchHistories
                    .Where(wh => wh.UserId == userId && wh.WatchDate >= subscription.StartDate)
                    .SumAsync(wh => wh.WatchCount);

                subscription.TotalWatched = totalWatched;
                await _context.SaveChangesAsync();

                Console.WriteLine($"TotalWatched updated for user {userId}: {subscription.TotalWatched}");
                return Json(new { success = true, totalWatched = subscription.TotalWatched });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IncrementVideosWatched error for user {userId}: {ex.Message}");
                return Json(new { success = false, message = "Error updating TotalWatched: " + ex.Message });
            }
        }
    }
}