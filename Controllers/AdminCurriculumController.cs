using System.Security.Claims;
using System.Text.RegularExpressions;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/learning-curriculum")]
public sealed class AdminCurriculumController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminCurriculumController> _logger;
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public AdminCurriculumController(ApplicationDbContext context, ILogger<AdminCurriculumController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new CurriculumManagementViewModel
        {
            AcademicYears = await _context.AcademicYears.AsNoTracking().OrderByDescending(item => item.Name).ToListAsync(cancellationToken),
            Classes = await _context.LearningClasses.AsNoTracking().Include(item => item.AcademicYear)
                .Include(item => item.Subjects).ThenInclude(item => item.Chapters).ThenInclude(item => item.Topics).ThenInclude(item => item.Lessons)
                .OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken),
            Subjects = await _context.Subjects.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken),
            Chapters = await _context.Chapters.AsNoTracking().Include(item => item.Subject).OrderBy(item => item.Title).ToListAsync(cancellationToken),
            Topics = await _context.Topics.AsNoTracking().Include(item => item.Chapter).ThenInclude(item => item.Subject).OrderBy(item => item.Title).ToListAsync(cancellationToken),
            Lessons = await _context.Lessons.AsNoTracking().Include(item => item.Topic).ThenInclude(item => item.Chapter).ThenInclude(item => item.Subject).ThenInclude(item => item.LearningClass).OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken)
        };
        return View(model);
    }

    [HttpPost("academic-year"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAcademicYear(string name, DateOnly startsOn, DateOnly endsOn, bool isCurrent, CancellationToken cancellationToken)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 40 || endsOn <= startsOn) return BadRequest("Enter a name and a valid academic-year date range.");
        if (await _context.AcademicYears.AnyAsync(item => item.Name == name, cancellationToken)) return BadRequest("That academic year already exists.");
        if (isCurrent)
            foreach (var year in await _context.AcademicYears.Where(item => item.IsCurrent).ToListAsync(cancellationToken)) year.IsCurrent = false;
        _context.AcademicYears.Add(new AcademicYear { Name = name, StartsOn = startsOn, EndsOn = endsOn, IsCurrent = isCurrent });
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("AcademicYear", name, "Created", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("class"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateClass(int academicYearId, string name, string slug, int displayOrder, CancellationToken cancellationToken)
    {
        if (!await _context.AcademicYears.AnyAsync(item => item.Id == academicYearId, cancellationToken)) return BadRequest("Select an academic year.");
        if (!ValidNameAndSlug(name, slug)) return BadRequest("Enter a class name and a lowercase URL slug.");
        slug = slug.Trim();
        if (await _context.LearningClasses.AnyAsync(item => item.Slug == slug, cancellationToken)) return BadRequest("Class slugs must be unique across academic years. Include the year in the slug when adding a historical version.");
        _context.LearningClasses.Add(new LearningClass { AcademicYearId = academicYearId, Name = name.Trim(), Slug = slug, DisplayOrder = displayOrder, IsPublished = false });
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Class", slug, "Created as draft", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("subject"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubject(int learningClassId, string name, string slug, int displayOrder, CancellationToken cancellationToken)
    {
        if (!await _context.LearningClasses.AnyAsync(item => item.Id == learningClassId, cancellationToken)) return BadRequest("Select a class.");
        if (!ValidNameAndSlug(name, slug)) return BadRequest("Enter a subject name and a lowercase URL slug.");
        slug = slug.Trim();
        if (await _context.Subjects.AnyAsync(item => item.LearningClassId == learningClassId && item.Slug == slug, cancellationToken)) return BadRequest("That subject already exists for this class.");
        _context.Subjects.Add(new Subject { LearningClassId = learningClassId, Name = name.Trim(), Slug = slug, DisplayOrder = displayOrder, IsPublished = false });
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Subject", slug, "Created as draft", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("chapter"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChapter(int subjectId, string title, int displayOrder, CancellationToken cancellationToken)
    {
        if (!await _context.Subjects.AnyAsync(item => item.Id == subjectId, cancellationToken) || string.IsNullOrWhiteSpace(title) || title.Trim().Length > 150) return BadRequest("Select a subject and enter a chapter title.");
        _context.Chapters.Add(new Chapter { SubjectId = subjectId, Title = title.Trim(), DisplayOrder = displayOrder, IsPublished = false });
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Chapter", title.Trim(), "Created as draft", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("topic"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(int chapterId, string title, string slug, int displayOrder, CancellationToken cancellationToken)
    {
        if (!await _context.Chapters.AnyAsync(item => item.Id == chapterId, cancellationToken) || !ValidNameAndSlug(title, slug)) return BadRequest("Select a chapter and enter a topic title and lowercase URL slug.");
        slug = slug.Trim();
        if (await _context.Topics.AnyAsync(item => item.ChapterId == chapterId && item.Slug == slug, cancellationToken)) return BadRequest("That topic already exists in this chapter.");
        _context.Topics.Add(new Topic { ChapterId = chapterId, Title = title.Trim(), Slug = slug, DisplayOrder = displayOrder, IsPublished = false });
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Topic", slug, "Created as draft", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("lesson"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLesson(int topicId, string title, string slug, string description, string summary, string learningObjectives, string keyConcepts, int durationMinutes, int displayOrder, CancellationToken cancellationToken)
    {
        if (!await _context.Topics.AnyAsync(item => item.Id == topicId, cancellationToken) || !ValidNameAndSlug(title, slug) ||
            string.IsNullOrWhiteSpace(description) || description.Length > 1200 || summary.Length > 4000 || keyConcepts.Length > 4000 ||
            string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(keyConcepts) || durationMinutes < 0 || durationMinutes > 600)
            return BadRequest("Provide a topic, lesson title, description, summary or key concepts, and a valid duration.");
        slug = slug.Trim();
        if (await _context.Lessons.AnyAsync(item => item.TopicId == topicId && item.Slug == slug, cancellationToken)) return BadRequest("That lesson slug is already used in this topic.");
        var lesson = new Lesson { TopicId = topicId, Title = title.Trim(), Slug = slug, Description = description.Trim(), Summary = summary.Trim(), LearningObjectives = learningObjectives?.Trim() ?? string.Empty, KeyConcepts = keyConcepts.Trim(), DurationMinutes = durationMinutes, DisplayOrder = displayOrder, IsPublished = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Lesson", lesson.Id.ToString(), "Created as draft", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("lesson/{id:int}/edit")]
    public async Task<IActionResult> EditLesson(int id, CancellationToken cancellationToken)
    {
        var lesson = await _context.Lessons.FindAsync([id], cancellationToken);
        return lesson == null ? NotFound() : View(new LessonEditViewModel { Id = lesson.Id, Title = lesson.Title, Slug = lesson.Slug, Description = lesson.Description, Summary = lesson.Summary, LearningObjectives = lesson.LearningObjectives, KeyConcepts = lesson.KeyConcepts, DurationMinutes = lesson.DurationMinutes, DisplayOrder = lesson.DisplayOrder });
    }

    [HttpPost("lesson/{id:int}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLesson(int id, LessonEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id || !ValidNameAndSlug(model.Title, model.Slug) || string.IsNullOrWhiteSpace(model.Description) || model.Description.Length > 1200 || model.Summary.Length > 4000 || model.KeyConcepts.Length > 4000 || model.DurationMinutes is < 0 or > 600) return BadRequest("Lesson details are invalid.");
        var lesson = await _context.Lessons.FindAsync([id], cancellationToken);
        if (lesson == null) return NotFound();
        if (lesson.Title != model.Title.Trim() || lesson.Slug != model.Slug.Trim() || lesson.Description != model.Description.Trim() || lesson.Summary != model.Summary.Trim() || lesson.LearningObjectives != model.LearningObjectives.Trim() || lesson.KeyConcepts != model.KeyConcepts.Trim() || lesson.DurationMinutes != model.DurationMinutes || lesson.DisplayOrder != model.DisplayOrder)
        {
            _context.LessonVersions.Add(new LessonVersion { LessonId = lesson.Id, Version = lesson.Version, Title = lesson.Title, Description = lesson.Description, Summary = lesson.Summary, LearningObjectives = lesson.LearningObjectives, KeyConcepts = lesson.KeyConcepts, DurationMinutes = lesson.DurationMinutes, DisplayOrder = lesson.DisplayOrder, CreatedAtUtc = lesson.UpdatedAtUtc });
            lesson.Version++;
        }
        lesson.Title = model.Title.Trim(); lesson.Slug = model.Slug.Trim(); lesson.Description = model.Description.Trim(); lesson.Summary = model.Summary.Trim();
        lesson.LearningObjectives = model.LearningObjectives.Trim(); lesson.KeyConcepts = model.KeyConcepts.Trim(); lesson.DurationMinutes = model.DurationMinutes; lesson.DisplayOrder = model.DisplayOrder; lesson.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Lesson", lesson.Id.ToString(), "Updated", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("lesson/{id:int}/publish"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLessonPublished(int id, bool published, CancellationToken cancellationToken)
    {
        var lesson = await _context.Lessons.Include(item => item.Topic).ThenInclude(item => item.Chapter).ThenInclude(item => item.Subject).ThenInclude(item => item.LearningClass).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lesson == null) return NotFound();
        if (published && string.IsNullOrWhiteSpace(lesson.Summary) && string.IsNullOrWhiteSpace(lesson.KeyConcepts)) return BadRequest("Add lesson summary or key concepts before publishing.");
        if (published && (!lesson.Topic.IsPublished || !lesson.Topic.Chapter.IsPublished || !lesson.Topic.Chapter.Subject.IsPublished || !lesson.Topic.Chapter.Subject.LearningClass.IsPublished)) return BadRequest("Publish the lesson's class, subject, chapter, and topic first.");
        lesson.IsPublished = published;
        lesson.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange("Lesson", lesson.Id.ToString(), published ? "Published" : "Unpublished", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("curriculum-node/publish"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetNodePublished(string type, int id, bool published, CancellationToken cancellationToken)
    {
        var valid = false;
        switch (type)
        {
            case "class":
                var learningClass = await _context.LearningClasses.FindAsync([id], cancellationToken);
                if (learningClass != null && (!published || learningClass.AcademicYearId.HasValue)) { learningClass.IsPublished = published; valid = true; }
                break;
            case "subject":
                var subject = await _context.Subjects.Include(item => item.LearningClass).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (subject != null && (!published || subject.LearningClass.IsPublished)) { subject.IsPublished = published; valid = true; }
                break;
            case "chapter":
                var chapter = await _context.Chapters.Include(item => item.Subject).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (chapter != null && (!published || chapter.Subject.IsPublished)) { chapter.IsPublished = published; valid = true; }
                break;
            case "topic":
                var topic = await _context.Topics.Include(item => item.Chapter).ThenInclude(item => item.Subject).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (topic != null && (!published || topic.Chapter.IsPublished && topic.Chapter.Subject.IsPublished)) { topic.IsPublished = published; valid = true; }
                break;
        }
        if (!valid) return BadRequest("Publish parent curriculum items first.");
        await _context.SaveChangesAsync(cancellationToken);
        await LogChange(type, id.ToString(), published ? "Published" : "Unpublished", cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private static bool ValidNameAndSlug(string name, string slug) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 180 && !string.IsNullOrWhiteSpace(slug) && SlugPattern.IsMatch(slug.Trim());
    private async Task LogChange(string entity, string id, string action, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        _logger.LogInformation("Admin {AdminId} {Action} {Entity} {EntityId}.", adminId, action, entity, id);
        _context.AdminAuditLogs.Add(new AdminAuditLog { AdminUserId = adminId, Action = action, EntityName = entity, EntityId = id, Summary = action, CreatedAtUtc = DateTime.UtcNow });
        await _context.SaveChangesAsync(cancellationToken);
    }
}
