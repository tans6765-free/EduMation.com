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

        var answer = await _aiLearningService.AskTutorAsync(request.Question, lesson, cancellationToken);
        return Json(new { message = answer });
    }
}

public record TutorRequest(string Question, int? LessonId);
