using EduMation.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Data;

/// <summary>Indexes metadata for PDFs in the configured library; it never copies or reads PDF contents.</summary>
public sealed class NctbBookCatalogSeeder(ApplicationDbContext db, IConfiguration configuration, IWebHostEnvironment environment, ILogger<NctbBookCatalogSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var configuredRoot = configuration["Nctb:RootPath"] ?? "Content/NCTB";
        var root = Path.GetFullPath(Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(environment.ContentRootPath, configuredRoot));
        if (!Directory.Exists(root)) return;
        try
        {
            var files = Directory.EnumerateFiles(root, "*.pdf", SearchOption.AllDirectories).ToArray();
            var indexed = await db.SourceBooks.Where(book => !book.IsManagedFile)
                .Select(book => new SourceBook { Id = book.Id, RelativePath = book.RelativePath, Subject = book.Subject, OriginalFileName = book.OriginalFileName, ClassLevel = book.ClassLevel, LanguageVersion = book.LanguageVersion, IsActive = book.IsActive })
                .ToListAsync(cancellationToken);
            var known = indexed.ToDictionary(book => book.RelativePath, StringComparer.OrdinalIgnoreCase);
            var diskPaths = files.Select(file => Path.GetRelativePath(root, file).Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var missing in indexed.Where(book => book.IsActive && !diskPaths.Contains(book.RelativePath)))
            {
                await db.SourceBooks.Where(book => book.Id == missing.Id).ExecuteUpdateAsync(update => update.SetProperty(book => book.IsActive, false), cancellationToken);
                missing.IsActive = false;
            }

            var added = 0;
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var classFolder = parts.Length >= 3 ? parts[^3] : "Needs review";
                var classLabel = NormalizeClass(classFolder);
                var version = parts.Length >= 2 ? parts[^2] : "Needs review";
                version = version.Contains("English", StringComparison.OrdinalIgnoreCase) ? "English Version" : version.Contains("Bangla", StringComparison.OrdinalIgnoreCase) ? "Bangla Version" : version;
                var fileName = Path.GetFileName(file);
                foreach (var legacy in indexed.Where(book => book.IsActive && book.OriginalFileName == fileName && book.ClassLevel == classLabel && book.LanguageVersion == version &&
                    (book.RelativePath.Split('/').Contains("..") || Path.IsPathRooted(book.RelativePath.Replace('/', Path.DirectorySeparatorChar)))))
                {
                    await db.SourceBooks.Where(book => book.Id == legacy.Id).ExecuteUpdateAsync(update => update.SetProperty(book => book.IsActive, false), cancellationToken);
                    legacy.IsActive = false;
                }
                if (known.TryGetValue(relative, out var registered))
                {
                    if (!registered.IsActive)
                    {
                        await db.SourceBooks.Where(book => book.Id == registered.Id).ExecuteUpdateAsync(update => update.SetProperty(book => book.IsActive, true), cancellationToken);
                        registered.IsActive = true;
                    }
                    if (string.IsNullOrWhiteSpace(registered.Subject))
                        await db.SourceBooks.Where(book => book.Id == registered.Id).ExecuteUpdateAsync(update => update.SetProperty(book => book.Subject, GuessSubject(Path.GetFileNameWithoutExtension(file))), cancellationToken);
                    continue;
                }
                db.SourceBooks.Add(new SourceBook
                {
                    OriginalFileName = fileName, RelativePath = relative, ClassLevel = classLabel,
                    LanguageVersion = version,
                    Subject = GuessSubject(Path.GetFileNameWithoutExtension(file)), AcademicYear = "2026",
                    VerificationStatus = "NEEDS_REVIEW", ExtractionStatus = "NOT_EXTRACTED"
                });
                known[relative] = new SourceBook { RelativePath = relative };
                if (++added % 100 == 0) await db.SaveChangesAsync(cancellationToken);
            }
            if (added > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Indexed {BookCount} new NCTB PDF entries from {BookRoot}.", added, root);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not index the configured NCTB PDF directory {BookRoot}.", root);
        }
    }

    private static string NormalizeClass(string value)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches(value, @"\d+").Select(match => match.Value).ToArray();
        return numbers.Length > 1 ? $"Class {numbers[0]}-{numbers[1]}" : numbers.Length == 1 ? $"Class {numbers[0]}" : value;
    }

    private static string GuessSubject(string filename)
    {
        var text = filename.ToLowerInvariant();
        var hints = new (string[] Keys, string Name)[]
        {
            (new[] { "bangla", "বাংলা" }, "Bangla"), (new[] { "english", "ইংরেজি" }, "English"),
            (new[] { "math", "গণিত" }, "Mathematics"), (new[] { "science", "বিজ্ঞান" }, "Science"),
            (new[] { "ict", "information and communication" }, "ICT"),
            (new[] { "islam", "ইসলাম" }, "Islamic Studies"), (new[] { "hindu", "হিন্দু" }, "Hindu Religion Studies"),
            (new[] { "physics", "পদার্থ" }, "Physics"), (new[] { "chemistry", "রসায়ন", "রসায়ন" }, "Chemistry"),
            (new[] { "biology", "জীববিজ্ঞান" }, "Biology"), (new[] { "account", "হিসাব" }, "Accounting"),
            (new[] { "history", "ইতিহাস" }, "History"), (new[] { "geography", "ভূগোল" }, "Geography")
        };
        return hints.FirstOrDefault(item => item.Keys.Any(text.Contains)).Name ?? "Needs review";
    }
}
