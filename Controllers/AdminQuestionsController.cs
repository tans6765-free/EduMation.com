using System.Security.Claims;
using System.Text.Json;
using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/questions")]
public sealed class AdminQuestionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAiLearningService _ai;
    private readonly ILogger<AdminQuestionsController> _logger;
    private readonly string _provider;
    private readonly string _model;
    private readonly IAiUsageRecorder _usage;

    public AdminQuestionsController(ApplicationDbContext context, IAiLearningService ai, ILogger<AdminQuestionsController> logger, IConfiguration configuration, IAiUsageRecorder usage)
    {
        _context = context;
        _ai = ai;
        _logger = logger;
        _provider = configuration["Ai:Provider"] ?? "Gemini";
        _model = configuration[$"{_provider}:Model"] ?? "configured";
        _usage = usage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var questions = await _context.Questions.AsNoTracking().Include(item => item.Lesson)
            .OrderBy(item => item.Status).ThenByDescending(item => item.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);
        return View(questions);
    }

    [HttpPost("generate/{lessonId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int lessonId, CancellationToken cancellationToken)
    {
        var lesson = await _context.Lessons.Include(item => item.Topic).ThenInclude(item => item.Chapter)
            .ThenInclude(item => item.Subject).ThenInclude(item => item.LearningClass)
            .FirstOrDefaultAsync(item => item.Id == lessonId, cancellationToken);
        if (lesson == null) return NotFound();
        var timer = Stopwatch.StartNew();
        var generated = await _ai.GenerateQuestionsAsync(lesson, cancellationToken);
        await _usage.RecordAsync("Question generation", timer.ElapsedMilliseconds, generated.Count > 0, cancellationToken);
        if (generated.Count == 0)
        {
            TempData["Message"] = "No questions were generated. Add lesson summary or key concepts and check the AI provider configuration.";
            return RedirectToAction("Index", "AdminCurriculum");
        }

        foreach (var item in generated.Where(PracticeQuestionViewModel.IsValid))
        {
            _context.Questions.Add(new Question
            {
                LessonId = lesson.Id,
                QuestionText = item.Prompt,
                QuestionType = item.QuestionType,
                MaximumMarks = item.MaximumMarks,
                OptionsJson = JsonSerializer.Serialize(item.Options),
                CorrectOption = item.CorrectOption,
                ExpectedAnswer = item.ExpectedAnswer,
                Explanation = item.Explanation,
                Difficulty = "Medium",
                AiProvider = _provider,
                AiModel = _model,
                Status = "AI Generated",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("Question generation", "Lesson", lesson.Id.ToString(), cancellationToken);
        TempData["Message"] = "Generated questions are saved as drafts for admin review.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/review")]
    public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
    {
        var question = await _context.Questions.FindAsync([id], cancellationToken);
        if (question == null) return NotFound();
        var options = JsonSerializer.Deserialize<string[]>(question.OptionsJson) ?? Array.Empty<string>();
        return View(new QuestionReviewViewModel { Id = id, QuestionText = question.QuestionText, QuestionType = question.QuestionType, MaximumMarks = question.MaximumMarks, ExpectedAnswer = question.ExpectedAnswer, OptionsText = string.Join(Environment.NewLine, options), CorrectOption = question.CorrectOption, Explanation = question.Explanation, Difficulty = question.Difficulty });
    }

    [HttpPost("{id:int}/review"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, QuestionReviewViewModel model, CancellationToken cancellationToken)
    {
        var options = (model.OptionsText ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var validType = PracticeQuestionViewModel.SupportedQuestionTypes.Contains(model.QuestionType) && model.MaximumMarks == PracticeQuestionViewModel.MarksFor(model.QuestionType) &&
            (PracticeQuestionViewModel.IsChoiceType(model.QuestionType)
                ? options.Length is >= 2 and <= 6 && model.CorrectOption >= 0 && model.CorrectOption < options.Length && (model.QuestionType != "True/False" || options.Length == 2)
                : options.Length == 0);
        if (id != model.Id || string.IsNullOrWhiteSpace(model.QuestionText) || model.QuestionText.Length > 4000 || !validType || string.IsNullOrWhiteSpace(model.ExpectedAnswer) || model.ExpectedAnswer.Length > 4000 || string.IsNullOrWhiteSpace(model.Explanation) || model.Explanation.Length > 4000 || model.Difficulty is not ("Easy" or "Medium" or "Hard"))
        {
            ModelState.AddModelError(string.Empty, "Review the prompt, two to six options, correct option, difficulty, and explanation.");
            return View(model);
        }
        var question = await _context.Questions.FindAsync([id], cancellationToken);
        if (question == null) return NotFound();
        question.QuestionText = model.QuestionText.Trim();
        question.QuestionType = model.QuestionType;
        question.MaximumMarks = model.MaximumMarks;
        question.OptionsJson = JsonSerializer.Serialize(options);
        question.CorrectOption = PracticeQuestionViewModel.IsChoiceType(model.QuestionType) ? model.CorrectOption : -1;
        question.ExpectedAnswer = model.ExpectedAnswer.Trim();
        question.Explanation = model.Explanation.Trim();
        question.Difficulty = model.Difficulty;
        question.Version++;
        question.Status = model.Decision == "reject" ? "Rejected" : "Approved";
        question.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("Question " + question.Status, "Question", id.ToString(), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/publish"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        var question = await _context.Questions.FindAsync([id], cancellationToken);
        if (question == null) return NotFound();
        if (question.Status != "Approved") return BadRequest("Only reviewed and approved questions can be published.");
        question.Status = "Published";
        question.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("Question published", "Question", id.ToString(), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task AuditAsync(string action, string entity, string id, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        _logger.LogInformation("Admin {AdminId} {Action} {Entity} {EntityId}.", adminId, action, entity, id);
        _context.AdminAuditLogs.Add(new AdminAuditLog { AdminUserId = adminId, Action = action, EntityName = entity, EntityId = id, Summary = action, CreatedAtUtc = DateTime.UtcNow });
        await _context.SaveChangesAsync(cancellationToken);
    }
}
