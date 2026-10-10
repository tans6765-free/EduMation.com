using System.Collections.Concurrent;
using EduMation.Data;
using EduMation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Route("books")]
public sealed class BooksController(ApplicationDbContext db, IWebHostEnvironment environment, IConfiguration configuration, ILogger<BooksController> logger) : Controller
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CoverLocks = new(StringComparer.OrdinalIgnoreCase);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? grade, string? version, string? subject, CancellationToken cancellationToken)
    {
        var query = db.SourceBooks.AsNoTracking().Where(book => book.IsActive);
        if (grade is >= 1 and <= 12)
        {
            var className = $"Class {grade.Value}";
            query = query.Where(book => book.ClassLevel == className || ((grade == 9 || grade == 10) && book.ClassLevel == "Class 9-10") || ((grade == 11 || grade == 12) && book.ClassLevel == "Class 11-12"));
        }
        if (!string.IsNullOrWhiteSpace(version)) query = query.Where(book => book.LanguageVersion == version);
        if (!string.IsNullOrWhiteSpace(subject)) query = query.Where(book => book.Subject == subject);
        ViewBag.Grade = grade; ViewBag.Version = version; ViewBag.Subject = subject;
        ViewBag.Subjects = await db.SourceBooks.AsNoTracking().Where(book => book.IsActive).Select(book => book.Subject).Distinct().OrderBy(name => name).ToListAsync(cancellationToken);
        ViewBag.TotalBooks = await db.SourceBooks.CountAsync(book => book.IsActive, cancellationToken);
        var books = await query.OrderBy(book => book.ClassLevel).ThenBy(book => book.Subject).ThenBy(book => book.OriginalFileName)
            .Select(book => new SourceBook { Id = book.Id, ClassLevel = book.ClassLevel, LanguageVersion = book.LanguageVersion, Subject = book.Subject, OriginalFileName = book.OriginalFileName, VerificationStatus = book.VerificationStatus, IsManagedFile = book.IsManagedFile })
            .ToListAsync(cancellationToken);
        foreach (var book in books) book.Subject = GetDisplayBookName(book);
        return View(books);
    }

    [HttpGet("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken cancellationToken) => await Serve(id, false, cancellationToken);

    [HttpGet("{id:int}/cover")]
    public async Task<IActionResult> Cover(int id, CancellationToken cancellationToken)
    {
        var book = await db.SourceBooks.AsNoTracking().Where(item => item.Id == id && item.IsActive)
            .Select(item => new { item.IsManagedFile, item.RelativePath })
            .FirstOrDefaultAsync(cancellationToken);
        if (book == null) return NotFound();

        var root = book.IsManagedFile ? Path.Combine(environment.ContentRootPath, "App_Data", "books") : GetRoot();
        var fullRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(fullRoot, book.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path)) return NotFound();

        var fileInfo = new FileInfo(path);
        var cacheRoot = Path.Combine(environment.ContentRootPath, "App_Data", "book-covers");
        var cachePath = Path.Combine(cacheRoot, $"page1-v2-{id}-{fileInfo.Length}-{fileInfo.LastWriteTimeUtc.Ticks}.png");
        var coverLock = CoverLocks.GetOrAdd(cachePath, _ => new SemaphoreSlim(1, 1));
        await coverLock.WaitAsync(cancellationToken);

        try
        {
            if (IsValidPngFile(cachePath)) return PhysicalFile(cachePath, "image/png");
            if (System.IO.File.Exists(cachePath)) System.IO.File.Delete(cachePath);

            Directory.CreateDirectory(cacheRoot);
            var temporaryPath = Path.Combine(cacheRoot, $"{Guid.NewGuid():N}.tmp.png");
            try
            {
            await using (var pdfStream = System.IO.File.OpenRead(path))
                    PDFtoImage.Conversion.SavePng(imageFilename: temporaryPath, pdfStream: pdfStream, page: 0);
                if (!IsValidPngFile(temporaryPath)) throw new InvalidDataException("PDF renderer produced an incomplete cover image.");
                System.IO.File.Move(temporaryPath, cachePath, true);
            }
            finally
            {
                if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath);
            }
            return PhysicalFile(cachePath, "image/png");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not extract a cover image from book {BookId}.", id);
            return NotFound();
        }
        finally
        {
            coverLock.Release();
        }
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken) => await Serve(id, true, cancellationToken);

    [Authorize(Roles = "Admin"), HttpPost("scan"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(CancellationToken cancellationToken)
    {
        var root = GetRoot();
        if (!Directory.Exists(root)) { TempData["Message"] = $"Book folder not found: {root}"; return RedirectToAction(nameof(Index)); }
        var added = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.pdf", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (await db.SourceBooks.AnyAsync(book => book.RelativePath == relative, cancellationToken)) continue;
            var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var classFolder = parts.Length >= 3 ? parts[^3] : "Needs review";
            var (level, classLabel) = NormalizeClass(classFolder);
            var version = parts.Length >= 2 ? parts[^2] : "Needs review";
            var subject = GuessSubject(Path.GetFileNameWithoutExtension(path));
            db.SourceBooks.Add(new SourceBook { OriginalFileName = Path.GetFileName(path), RelativePath = relative, ClassLevel = classLabel, LanguageVersion = NormalizeVersion(version), Subject = subject, AcademicYear = "2026", VerificationStatus = "NEEDS_REVIEW", ExtractionStatus = "NOT_EXTRACTED" });
            added++;
            if (added % 100 == 0) await db.SaveChangesAsync(cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = $"Indexed {added} new PDF books. The original files remain in their source folder.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin"), HttpPost("upload"), RequestSizeLimit(300L * 1024 * 1024), ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile pdf, int grade, string version, string subject, CancellationToken cancellationToken)
    {
        if (pdf is not { Length: > 4 and <= 250L * 1024 * 1024 } || !Path.GetExtension(pdf.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) || grade is < 1 or > 12 || !new[] { "Bangla Version", "English Version" }.Contains(version) || string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 160)
        { TempData["Message"] = "Choose a PDF under 250 MB and provide a valid class, version, and subject."; return RedirectToAction(nameof(Index)); }
        await using var input = pdf.OpenReadStream();
        var signature = new byte[5];
        if (await input.ReadAsync(signature, cancellationToken) != 5 || !signature.AsSpan().SequenceEqual("%PDF-"u8))
        { TempData["Message"] = "The uploaded file is not a valid PDF."; return RedirectToAction(nameof(Index)); }
        var originalName = Path.GetFileName(pdf.FileName);
        var safeFile = string.Concat(originalName.Where(character => !Path.GetInvalidFileNameChars().Contains(character)));
        if (safeFile.Length > 100) safeFile = safeFile[^100..];
        var folderName = $"Class {grade}";
        var relative = Path.Combine("managed", folderName, version, $"{Guid.NewGuid():N}_{safeFile}").Replace('\\', '/');
        var managedRoot = Path.Combine(environment.ContentRootPath, "App_Data", "books");
        var destination = Path.GetFullPath(Path.Combine(managedRoot, relative));
        if (!destination.StartsWith(Path.GetFullPath(managedRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return BadRequest();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using (var output = System.IO.File.Create(destination)) { input.Position = 0; await pdf.CopyToAsync(output, cancellationToken); }
        db.SourceBooks.Add(new SourceBook { OriginalFileName = safeFile, RelativePath = relative, ClassLevel = folderName, LanguageVersion = version, Subject = subject.Trim(), VerificationStatus = "ADMIN_UPLOADED", IsManagedFile = true, AcademicYear = "2026", ExtractionStatus = "NOT_EXTRACTED" });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "Book PDF uploaded.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin"), HttpPost("{id:int}/remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        var book = await db.SourceBooks.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.Id, item.RelativePath, item.IsManagedFile })
            .FirstOrDefaultAsync(cancellationToken);
        if (book == null) return NotFound();
        if (book.IsManagedFile)
        {
            var managedRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "books"));
            var path = Path.GetFullPath(Path.Combine(managedRoot, book.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (path.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        await db.SourceBooks.Where(item => item.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsActive, false), cancellationToken);
        TempData["Message"] = book.IsManagedFile ? "Managed book removed." : "Book removed from the library; original source file was kept.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Serve(int id, bool attachment, CancellationToken cancellationToken)
    {
        var book = await db.SourceBooks.AsNoTracking().Where(item => item.Id == id && item.IsActive)
            .Select(item => new { item.Id, item.IsManagedFile, item.RelativePath, item.OriginalFileName })
            .FirstOrDefaultAsync(cancellationToken);
        if (book == null) return NotFound();
        var root = book.IsManagedFile ? Path.Combine(environment.ContentRootPath, "App_Data", "books") : GetRoot();
        var fullRoot = Path.GetFullPath(root);
        var path = Path.GetFullPath(Path.Combine(fullRoot, book.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path)) return NotFound();
        try { return PhysicalFile(path, "application/pdf", attachment ? book.OriginalFileName : null, enableRangeProcessing: true); }
        catch (Exception ex) { logger.LogError(ex, "Could not serve catalogued book {BookId}.", id); return NotFound(); }
    }

    private string GetRoot()
    {
        var configured = configuration["Nctb:RootPath"] ?? "Content/NCTB";
        return Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
    }
    private static bool IsValidPngFile(string path)
    {
        try
        {
            using var stream = System.IO.File.OpenRead(path);
            if (stream.Length < 45) return false;
            Span<byte> signature = stackalloc byte[8];
            Span<byte> end = stackalloc byte[12];
            return stream.Read(signature) == signature.Length && signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
                stream.Seek(-end.Length, SeekOrigin.End) >= 0 && stream.Read(end) == end.Length &&
                end[..8].SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68 });
        }
        catch (IOException)
        {
            return false;
        }
    }
    private static (int? Level, string Label) NormalizeClass(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\d+");
        if (!match.Success || !int.TryParse(match.Value, out var start)) return (null, value);
        var endMatch = System.Text.RegularExpressions.Regex.Match(value, @"[-–]\s*(\d+)");
        var label = endMatch.Success ? $"Class {start}-{endMatch.Groups[1].Value}" : $"Class {start}";
        return (start, label);
    }
    private static string NormalizeVersion(string value) => value.Contains("English", StringComparison.OrdinalIgnoreCase) ? "English Version" : value.Contains("Bangla", StringComparison.OrdinalIgnoreCase) ? "Bangla Version" : value;
    private static string GetDisplayBookName(SourceBook book)
    {
        var fileName = Path.GetFileNameWithoutExtension(book.OriginalFileName).ToLowerInvariant();
        var isEnglishVersion = book.LanguageVersion == "English Version";
        var isPrimaryOneOrTwo = book.ClassLevel is "Class 1" or "Class 2";
        string Localized(string bangla, string english) => isEnglishVersion ? english : bangla;

        if (ContainsAny(fileName, "home science", "homescience")) return Localized("গার্হস্থ্য বিজ্ঞান", "Home Science");
        if (book.ClassLevel == "Class 4" && ContainsAny(fileName, "math_g4_bangla")) return "প্রাথমিক গণিত";
        if (book.ClassLevel is "Class 4" or "Class 5" && ContainsAny(fileName, "hindu religious english")) return "Hindu Religion and Moral Education";
        if (book.ClassLevel is "Class 3" or "Class 4" or "Class 5" && isEnglishVersion && ContainsAny(fileName, "bangla", "my bangla")) return "আমার বাংলা বই";
        if (book.ClassLevel == "Class 11-12" && ContainsAny(fileName, "ict"))
            return Localized("তথ্য ও যোগাযোগ প্রযুক্তি", "Information and Communication Technology");
        if (book.ClassLevel == "Class 11-12" && isEnglishVersion)
        {
            if (ContainsAny(fileName, "সহপাঠ", "sohopath")) return "সহপাঠ";
            if (ContainsAny(fileName, "সাহিত্য", "sahitto")) return "সাহিত্যপাঠ";
        }

        if (book.ClassLevel == "Class 9-10")
        {
            if (ContainsAny(fileName, "higher math")) return Localized("উচ্চতর গণিত", "Higher Mathematics");
            if (isEnglishVersion && ContainsAny(fileName, "bangla grammar")) return "বাংলা ব্যাকরণ ও নির্মিতি";
            if (isEnglishVersion && ContainsAny(fileName, "bangla sahitto")) return "সাহিত্যপাঠ";
            if (ContainsAny(fileName, "bangla sohopath")) return "বাংলা সহপাঠ";
            if (isEnglishVersion && ContainsAny(fileName, "arbi")) return "সহজ আরবি পাঠ";
            if (isEnglishVersion && ContainsAny(fileName, "pali")) return "পালি";
            if (isEnglishVersion && ContainsAny(fileName, "songgit")) return "সংগীত";
            if (isEnglishVersion && ContainsAny(fileName, "songskrito")) return "সংস্কৃত";
        }

        if (book.ClassLevel is "Class 6" or "Class 7" or "Class 8" && isEnglishVersion)
        {
            if (ContainsAny(fileName, "bangla grammar", "bekoron", "bakoron")) return "বাংলা ব্যাকরণ ও নির্মিতি";
            if (ContainsAny(fileName, "anand", "anondopath")) return "আনন্দপাঠ";
            if (ContainsAny(fileName, "arbi", "arabic")) return "সহজ আরবি পাঠ";
            if (ContainsAny(fileName, "charupath")) return "চারুপাঠ";
            if (book.ClassLevel == "Class 7" && ContainsAny(fileName, "saptabarna")) return "সপ্তবর্ণা";
            if (book.ClassLevel == "Class 8" && ContainsAny(fileName, "sahitto")) return "সাহিত্য-কণিকা";
            if (ContainsAny(fileName, "pali")) return "পালি";
            if (ContainsAny(fileName, "songskrito")) return "সংস্কৃত";
            if (ContainsAny(fileName, "songit", "songeet", "songgit")) return "সংগীত";
        }

        if (ContainsAny(fileName, "english grammar", "english gerammar", "ইংরেজি ব্যাকরণ")) return "English Grammar and Composition";
        if (ContainsAny(fileName, "bangla grammar", "বাংলা ব্যাকরণ", "bekoron", "bakoron"))
            return book.ClassLevel is "Class 6" or "Class 7" or "Class 8" && isEnglishVersion
                ? "বাংলা ব্যাকরণ ও নির্মিতি"
                : Localized("বাংলা ব্যাকরণ ও নির্মিতি", "Bangla Grammar and Composition");
        if (ContainsAny(fileName, "anand", "anondopath", "আনন্দপাঠ", "sohopath", "সহপাঠ"))
            return book.ClassLevel == "Class 9-10" ? "বাংলা সহপাঠ" : book.ClassLevel == "Class 11-12" ? Localized("সহপাঠ", "Supplementary Reader") : Localized("আনন্দপাঠ", "Anandapath");
        if (ContainsAny(fileName, "charupath", "চারুপাঠ")) return Localized("চারুপাঠ", "Bangla Literature Reader");
        if (ContainsAny(fileName, "saptabarna", "সপ্তবর্ণা")) return Localized("সপ্তবর্ণা", "Saptabarna");
        if (ContainsAny(fileName, "sahitto", "সাহিত্য"))
        {
            if (book.ClassLevel == "Class 8") return "সাহিত্য-কণিকা";
            return Localized("সাহিত্যপাঠ", "Bangla Literature");
        }
        if (isEnglishVersion && book.Subject == "Bangla")
        {
            if (book.ClassLevel == "Class 6") return "চারুপাঠ";
            if (book.ClassLevel == "Class 7") return "সপ্তবর্ণা";
            if (book.ClassLevel == "Class 8") return "সাহিত্য-কণিকা";
        }

        var subject = book.Subject;
        if (subject == "Needs review")
        {
            if (ContainsAny(fileName, "bgs", "বাংলাদেশ ও বিশ্ব", "বিশ্বপরিচয়", "বিশ্বপরিচয়")) subject = "Social Studies";
            else if (ContainsAny(fileName, "budd", "buddis", "বৌদ্ধ")) subject = "Buddhist Religion Studies";
            else if (ContainsAny(fileName, "khristo", "christ", "cristian", "chirstian", "খ্রিস্ট", "খ্রিষ্ট")) subject = "Christian Religion Studies";
            else if (ContainsAny(fileName, "agriculture", "krishi", "কৃষি")) subject = "Agriculture Studies";
            else if (ContainsAny(fileName, "home science", "homescience", "গার্হস্থ্য")) subject = "Home Science";
            else if (ContainsAny(fileName, "arts", "craft", "চারু", "কারুকলা")) subject = "Arts and Crafts";
            else if (ContainsAny(fileName, "history", "ইতিহাস")) subject = "History";
            else if (ContainsAny(fileName, "geography", "bhugol", "ভূগোল")) subject = "Geography";
            else if (ContainsAny(fileName, "pali", "পালি")) subject = "Pali";
            else if (ContainsAny(fileName, "sanskrit", "songskrito", "সংস্কৃত")) subject = "Sanskrit";
            else if (ContainsAny(fileName, "arabic", "arbi", "আরবি")) subject = "Arabic Studies";
            else if (ContainsAny(fileName, "music", "songit", "songeet", "songgit", "songgit", "সংগীত")) subject = "Music";
            else if (ContainsAny(fileName, "physical education", "phy edu", "sharirik sikha", "শারীরিক শিক্ষা")) subject = "Physical Education and Health";
            else if (ContainsAny(fileName, "work and life", "work & life", "work_life", "kormo o jibon", "কর্ম ও জীবন")) subject = "Work and Life-Oriented Education";
            else if (ContainsAny(fileName, "ethnic", "minority", "ক্ষুদ্র নৃগোষ্ঠী")) subject = "Minority Ethnic Languages and Culture";
            else if (ContainsAny(fileName, "carrer", "career", "ক্যারিয়ার", "ক্যারিয়ার")) subject = "Career Education";
            else if (ContainsAny(fileName, "civics", "পৌরনীতি")) subject = "Civics and Citizenship";
            else if (ContainsAny(fileName, "economics", "অর্থনীতি")) subject = "Economics";
            else if (ContainsAny(fileName, "finance", "ফিন্যান্স")) subject = "Finance and Banking";
            else if (ContainsAny(fileName, "babsy uddog", "business entrepreneurship", "ব্যবসায় উদ্যোগ")) subject = "Business Entrepreneurship";
        }

        return subject switch
        {
            "Bangla" => isPrimaryOneOrTwo
                ? "আমার বাংলা বই"
                : book.ClassLevel is "Class 3" or "Class 4" or "Class 5"
                    ? "আমার বাংলা বই"
                    : book.ClassLevel == "Class 6"
                        ? Localized("চারুপাঠ", "Bangla Literature Reader")
                        : book.ClassLevel == "Class 7"
                            ? Localized("সপ্তবর্ণা", "Saptabarna")
                            : Localized("সাহিত্যপাঠ", "Bangla Literature"),
            "English" => ContainsAny(fileName, "grammar", "ব্যাকরণ") ? "English Grammar and Composition" : "English for Today",
            "Mathematics" => book.ClassLevel == "Class 9-10" && ContainsAny(fileName, "higher math")
                ? Localized("উচ্চতর গণিত", "Higher Mathematics")
                : isPrimaryOneOrTwo
                ? isEnglishVersion ? "Elementary Mathematics" : "প্রাথমিক গণিত"
                : book.ClassLevel is "Class 3" or "Class 4" or "Class 5"
                    ? Localized("প্রাথমিক গণিত", "Elementary Mathematics")
                    : Localized("গণিত", "Mathematics"),
            "Science" => book.ClassLevel is "Class 3" or "Class 4" or "Class 5"
                ? Localized("প্রাথমিক বিজ্ঞান", "Elementary Science")
                : Localized("বিজ্ঞান", "Science"),
            "Social Studies" => Localized("বাংলাদেশ ও বিশ্বপরিচয়", "Bangladesh and Global Studies"),
            "ICT" => book.ClassLevel == "Class 11-12" && isEnglishVersion
                ? "Information and Communication Technology (English Version)"
                : Localized("তথ্য ও যোগাযোগ প্রযুক্তি", "Information and Communication Technology"),
            "Islamic Studies" or "Islam and Moral Education" => Localized("ইসলাম ও নৈতিক শিক্ষা", "Islamic Studies and Moral Education"),
            "Hindu Religion Studies" => Localized("হিন্দুধর্ম ও নৈতিক শিক্ষা", "Hindu Religion and Moral Education"),
            "Buddhist Religion Studies" => Localized("বৌদ্ধধর্ম ও নৈতিক শিক্ষা", "Buddhist Religion and Moral Education"),
            "Christian Religion Studies" => Localized("খ্রিষ্টধর্ম ও নৈতিক শিক্ষা", "Christian Religion and Moral Education"),
            "Physics" => Localized("পদার্থবিজ্ঞান", "Physics"),
            "Chemistry" => Localized("রসায়ন", "Chemistry"),
            "Biology" => Localized("জীববিজ্ঞান", "Biology"),
            "Accounting" => Localized("হিসাববিজ্ঞান", "Accounting"),
            "Finance and Banking" => Localized("ফিন্যান্স ও ব্যাংকিং", "Finance and Banking"),
            "Business Entrepreneurship" => Localized("ব্যবসায় উদ্যোগ", "Business Entrepreneurship"),
            "History" => book.ClassLevel == "Class 9-10"
                ? "বাংলাদেশের ইতিহাস ও বিশ্বসভ্যতা"
                : Localized("ইতিহাস ও বিশ্বসভ্যতা", "History of Bangladesh and World Civilization"),
            "Geography" => Localized("ভূগোল ও পরিবেশ", "Geography and Environment"),
            "Agriculture Studies" => Localized("কৃষিশিক্ষা", "Agriculture Studies"),
            "Home Science" => Localized("গার্হস্থ্যবিজ্ঞান", "Home Science"),
            "Arts and Crafts" => Localized("চারু ও কারুকলা", "Arts and Crafts"),
            "Pali" => Localized("পালি", "Pali"),
            "Sanskrit" => Localized("সংস্কৃত", "Sanskrit"),
            "Arabic Studies" => Localized("সহজ আরবি পাঠ", "Simple Arabic Reader"),
            "Music" => Localized("সংগীত", "Music"),
            "Physical Education and Health" => Localized("শারীরিক শিক্ষা ও স্বাস্থ্য", "Physical Education and Health"),
            "Work and Life-Oriented Education" => Localized("কর্ম ও জীবনমুখী শিক্ষা", "Work and Life-Oriented Education"),
            "Minority Ethnic Languages and Culture" => Localized("ক্ষুদ্র নৃগোষ্ঠীর ভাষা ও সংস্কৃতি", "Minority Ethnic Languages and Culture"),
            "Career Education" => Localized("ক্যারিয়ার শিক্ষা", "Career Education"),
            "Civics and Citizenship" => Localized("পৌরনীতি ও নাগরিকতা", "Civics and Citizenship"),
            "Economics" => Localized("অর্থনীতি", "Economics"),
            "Needs review" => isEnglishVersion ? "Book title needs review" : "বইয়ের নাম যাচাই প্রয়োজন",
            _ => subject
        };
    }

    private static bool ContainsAny(string value, params string[] terms) => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    private static string GuessSubject(string name)
    {
        var text = name.ToLowerInvariant();
        var known = new (string[] Terms, string Subject)[] { (new[] { "bangla", "বাংলা" }, "Bangla"), (new[] { "english", "ইংরেজি" }, "English"), (new[] { "math", "গণিত" }, "Mathematics"), (new[] { "science", "বিজ্ঞান" }, "Science"), (new[] { "ict", "তথ্য ও যোগাযোগ" }, "ICT"), (new[] { "islam", "ইসলাম" }, "Islam and Moral Education"), (new[] { "hindu", "হিন্দুধর্ম" }, "Hindu Religion Studies"), (new[] { "social", "বাংলাদেশ ও বিশ্ব" }, "Social Studies"), (new[] { "physics", "পদার্থ" }, "Physics"), (new[] { "chemistry", "রসায়ন" }, "Chemistry"), (new[] { "biology", "জীববিজ্ঞান" }, "Biology"), (new[] { "account", "হিসাববিজ্ঞান" }, "Accounting"), (new[] { "finance", "ফিন্যান্স" }, "Finance and Banking"), (new[] { "business", "ব্যবসায়" }, "Business Entrepreneurship") };
        return known.FirstOrDefault(item => item.Terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))).Subject ?? "Needs review";
    }
}
