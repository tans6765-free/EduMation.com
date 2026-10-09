using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/curriculum")]
public class CurriculumController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly NctbContentOptions _options;

    public CurriculumController(ApplicationDbContext context, IWebHostEnvironment environment, IOptions<NctbContentOptions> options)
    {
        _context = context;
        _environment = environment;
        _options = options.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var root = GetSourceRoot();
        var files = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.pdf", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(_environment.ContentRootPath, path).Replace('\\', '/'))
                .OrderBy(path => path)
                .ToList()
            : new List<string>();

        return View(new CurriculumImportViewModel
        {
            RegisteredBooks = await _context.SourceBooks.OrderByDescending(book => book.ImportedAtUtc).ToListAsync(),
            DiscoveredFiles = files
        });
    }

    [HttpPost("scan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan()
    {
        var root = GetSourceRoot();
        if (!Directory.Exists(root))
        {
            TempData["Message"] = "The NCTB source folder does not exist yet.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.pdf", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(_environment.ContentRootPath, path).Replace('\\', '/');
            if (await _context.SourceBooks.AnyAsync(book => book.RelativePath == relativePath)) continue;

            var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var classLevel = parts.Length > 2 ? parts[^3] : "Needs review";
            var language = parts.Length > 1 ? parts[^2] : "Needs review";
            _context.SourceBooks.Add(new SourceBook
            {
                OriginalFileName = Path.GetFileName(path),
                RelativePath = relativePath,
                ClassLevel = classLevel,
                LanguageVersion = language,
                VerificationStatus = "NEEDS_REVIEW"
            });
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = "NCTB PDFs scanned. Review source metadata before importing chapters.";
        return RedirectToAction(nameof(Index));
    }

    private string GetSourceRoot() => Path.IsPathRooted(_options.RootPath)
        ? _options.RootPath
        : Path.Combine(_environment.ContentRootPath, _options.RootPath);
}
