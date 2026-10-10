using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using System.Diagnostics;
using System.Security.Claims;

namespace EduMation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var viewModel = new HomeLandingViewModel
            {
                Classes = await _context.LearningClasses
                    .Where(learningClass => learningClass.IsPublished)
                    .Include(learningClass => learningClass.Subjects)
                    .OrderBy(learningClass => learningClass.DisplayOrder)
                    .Take(6)
                    .ToListAsync(),
                FeaturedLessons = await _context.Lessons
                    .Where(lesson => lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished)
                    .Include(lesson => lesson.Topic)
                        .ThenInclude(topic => topic.Chapter)
                    .OrderBy(lesson => lesson.DisplayOrder)
                    .Take(3)
                    .ToListAsync()
            };

            if (userId != null)
            {
                viewModel.ContinueLesson = await _context.LessonProgresses.AsNoTracking()
                    .Where(progress => progress.UserId == userId && !progress.IsCompleted && progress.PercentComplete > 0)
                    .Include(progress => progress.Lesson).ThenInclude(lesson => lesson.Topic)
                    .OrderByDescending(progress => progress.LastViewedAt).FirstOrDefaultAsync();
            }

            return View(viewModel);
        }

        public Task<IActionResult> About() => RenderPublicPage("about", "About");

        public Task<IActionResult> HowItWorks() => RenderPublicPage("how-it-works", "HowItWorks");

        public Task<IActionResult> Contact() => RenderPublicPage("contact", "Contact");

        public Task<IActionResult> Privacy() => RenderPublicPage("privacy", "Privacy");

        [HttpGet("/pages/{slug}")]
        public async Task<IActionResult> Page(string slug, CancellationToken cancellationToken)
        {
            var page = await _context.SitePages.AsNoTracking().FirstOrDefaultAsync(item => item.Slug == slug && item.IsPublished, cancellationToken);
            if (page == null) return NotFound();
            ViewData["Title"] = page.Title; ViewData["MetaDescription"] = page.MetaDescription;
            return View("Page", page);
        }

        private async Task<IActionResult> RenderPublicPage(string slug, string viewName)
        {
            var page = await _context.SitePages.AsNoTracking().FirstOrDefaultAsync(item => item.Slug == slug && item.IsPublished);
            if (page == null) return NotFound();
            ViewData["Title"] = page.Title; ViewData["MetaDescription"] = page.MetaDescription;
            ViewData["SupportEmail"] = _configuration["Support:Email"];
            return View(viewName, page);
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var urls = await _context.Lessons.AsNoTracking()
                .Where(lesson => lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished)
                .Select(lesson => Url.Action("Lesson", "Learn", new { id = lesson.Id }))
                .ToListAsync();
            var publicPages = await _context.SitePages.AsNoTracking().Where(page => page.IsPublished)
                .Select(page => Url.Action("Page", "Home", new { slug = page.Slug })).ToListAsync();
            var entries = new[] { Url.Action("Index", "Home"), Url.Action("About", "Home"), Url.Action("HowItWorks", "Home"), Url.Action("Contact", "Home"), Url.Action("Index", "Learn"), Url.Action("Curriculum", "Learn") }
                .Concat(publicPages).Concat(urls).Where(url => !string.IsNullOrWhiteSpace(url)).Distinct();
            var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">" +
                string.Concat(entries.Select(url => $"<url><loc>{System.Security.SecurityElement.Escape($"{Request.Scheme}://{Request.Host}{url}")}</loc></url>")) + "</urlset>";
            return Content(xml, "application/xml");
        }

        [Route("robots.txt")]
        public IActionResult Robots()
        {
            var sitemapUrl = $"{Request.Scheme}://{Request.Host}/sitemap.xml";
            return Content($"User-agent: *\nAllow: /\nDisallow: /profile\nDisallow: /favorites\nDisallow: /watchhistory\nDisallow: /admin\nDisallow: /progress\nSitemap: {sitemapUrl}\n", "text/plain");
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
