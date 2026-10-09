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
    private readonly NctbPdfExtractionService _extractor;

    public CurriculumController(ApplicationDbContext context, IWebHostEnvironment environment, IOptions<NctbContentOptions> options, NctbPdfExtractionService extractor)
    {
        _context = context;
        _environment = environment;
        _options = options.Value;
        _extractor = extractor;
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
            var existing = await _context.SourceBooks.FirstOrDefaultAsync(book => book.RelativePath == relativePath);
            if (existing != null && existing.ExtractionStatus == "EXTRACTED") continue;

            var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var classLevel = parts.Length > 2 ? parts[^3] : "Needs review";
            var language = parts.Length > 1 ? parts[^2] : "Needs review";
            var extraction = _extractor.Extract(path);
            var sourceBook = existing ?? new SourceBook
            {
                OriginalFileName = Path.GetFileName(path),
                RelativePath = relativePath,
                ClassLevel = classLevel,
                LanguageVersion = language,
                VerificationStatus = "NEEDS_REVIEW"
            };
            sourceBook.PageCount = extraction.PageCount;
            sourceBook.ExtractedText = extraction.Text;
            sourceBook.ExtractionStatus = extraction.Status;
            if (existing == null) _context.SourceBooks.Add(sourceBook);
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = "NCTB PDFs scanned. Review source metadata before importing chapters.";
        return RedirectToAction(nameof(Index));
    }

    private string GetSourceRoot() => Path.IsPathRooted(_options.RootPath)
        ? _options.RootPath
        : Path.Combine(_environment.ContentRootPath, _options.RootPath);
}
