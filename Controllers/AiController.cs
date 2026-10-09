using EduMation.Data;
using EduMation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Route("ai")]
public class AiController : Controller
{
    private readonly IAiLearningService _aiLearningService;
    private readonly ApplicationDbContext _context;

    public AiController(IAiLearningService aiLearningService, ApplicationDbContext context)
    {
        _aiLearningService = aiLearningService;
        _context = context;
    }

    [HttpGet("chat")]
    public IActionResult Chat()
    {
        return View();
    }

    [HttpPost("chat/message")]
    public async Task<IActionResult> Message([FromBody] TutorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { message = "Ask a question about what you are learning." });
        }

        var lesson = request.LessonId.HasValue
            ? await _context.Lessons
                .Include(item => item.Topic)
                    .ThenInclude(topic => topic.Chapter)
                        .ThenInclude(chapter => chapter.Subject)
                            .ThenInclude(subject => subject.LearningClass)
                .FirstOrDefaultAsync(item => item.Id == request.LessonId.Value, cancellationToken)
            : null;

        var keyword = request.Question.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(word => word.Length >= 5 && word.All(char.IsLetter));
        var source = keyword == null
            ? null
            : await _context.SourceBooks.AsNoTracking()
                .Where(book => book.ExtractionStatus == "EXTRACTED" && book.ExtractedText.Contains(keyword))
                .Select(book => new { book.OriginalFileName, book.ClassLevel, book.LanguageVersion, book.ExtractedText })
                .FirstOrDefaultAsync(cancellationToken);
        var sourceContext = source == null
            ? null
            : $"Book: {source.OriginalFileName}; Class: {source.ClassLevel}; Language: {source.LanguageVersion}; Text excerpt: {TrimExcerpt(source.ExtractedText, keyword!)}";

        var answer = await _aiLearningService.AskTutorAsync(request.Question, lesson, sourceContext, cancellationToken);
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
