using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EduMation.Controllers;

[Route("ai")]
public class AiController : Controller
{
    private readonly IAiLearningService _aiLearningService;
    private readonly ApplicationDbContext _context;
    private readonly IAiUsageRecorder _usage;

    public AiController(IAiLearningService aiLearningService, ApplicationDbContext context, IAiUsageRecorder usage)
    {
        _aiLearningService = aiLearningService;
        _context = context;
        _usage = usage;
    }

    [HttpGet("chat")]
    public async Task<IActionResult> Chat(int? lessonId, CancellationToken cancellationToken)
    {
        if (lessonId.HasValue && !await _context.Lessons.AnyAsync(item => item.Id == lessonId && item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished, cancellationToken)) return NotFound();
        ViewBag.LessonId = lessonId;
        return View();
    }

    [HttpPost("chat/message")]
    public async Task<IActionResult> Message([FromBody] TutorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000)
        {
            return BadRequest(new { message = "Ask a question about what you are learning." });
        }

        var lesson = request.LessonId.HasValue
            ? await _context.Lessons
                .Include(item => item.Topic)
                    .ThenInclude(topic => topic.Chapter)
                        .ThenInclude(chapter => chapter.Subject)
                            .ThenInclude(subject => subject.LearningClass)
                .FirstOrDefaultAsync(item => item.Id == request.LessonId.Value && item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished, cancellationToken)
            : null;

        var keyword = request.Question.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(word => word.Length >= 5 && word.All(char.IsLetter));
        var source = keyword == null || lesson == null
            ? null
            : await _context.SourceBooks.AsNoTracking()
                .Where(book => book.IsActive && book.VerificationStatus == "OFFICIAL_PDF_VERIFIED" && book.ExtractionStatus == "EXTRACTED" &&
                    book.ClassLevel == lesson.Topic.Chapter.Subject.LearningClass.Name && book.Subject == lesson.Topic.Chapter.Subject.Name && book.ExtractedText.Contains(keyword))
                .Select(book => new { book.OriginalFileName, book.ClassLevel, book.LanguageVersion, book.ExtractedText })
                .FirstOrDefaultAsync(cancellationToken);
        var sourceContext = source == null
            ? null
            : $"Book: {source.OriginalFileName}; Class: {source.ClassLevel}; Language: {source.LanguageVersion}; Text excerpt: {TrimExcerpt(source.ExtractedText, keyword!)}";

        var timer = Stopwatch.StartNew();
        var answer = await _aiLearningService.AskTutorAsync(request.Question, lesson, sourceContext, cancellationToken);
        await _usage.RecordAsync("Tutor", timer.ElapsedMilliseconds, !answer.Contains("unavailable", StringComparison.OrdinalIgnoreCase) && !answer.Contains("could not reach", StringComparison.OrdinalIgnoreCase), cancellationToken);
        return Json(new { message = answer });
    }

    private static string TrimExcerpt(string text, string keyword)
    {
        var position = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, position - 3500);
        return text.Substring(start, Math.Min(7000, text.Length - start));
    }
}

public record TutorRequest(string Question, int? LessonId);
