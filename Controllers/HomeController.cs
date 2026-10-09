using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
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
            var viewModel = new HomeLandingViewModel
            {
                Classes = await _context.LearningClasses
                    .Where(learningClass => learningClass.IsPublished)
                    .Include(learningClass => learningClass.Subjects)
                    .OrderBy(learningClass => learningClass.DisplayOrder)
                    .Take(6)
                    .ToListAsync(),
                FeaturedLessons = await _context.Lessons
                    .Where(lesson => lesson.IsPublished)
                    .Include(lesson => lesson.Topic)
                        .ThenInclude(topic => topic.Chapter)
                    .OrderBy(lesson => lesson.DisplayOrder)
                    .Take(3)
                    .ToListAsync()
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