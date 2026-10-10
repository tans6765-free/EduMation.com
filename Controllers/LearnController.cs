using System.Security.Claims;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using EduMation.Services;
using System.Diagnostics;

namespace EduMation.Controllers;

[Route("learn")]
public class LearnController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IAiLearningService _ai;
    private readonly IAiUsageRecorder _aiUsage;
    private readonly IStudentEngagementService _engagement;

    public LearnController(ApplicationDbContext context, IMemoryCache cache, IAiLearningService ai, IAiUsageRecorder aiUsage, IStudentEngagementService engagement)
    {
        _context = context;
        _cache = cache;
        _ai = ai;
        _aiUsage = aiUsage;
        _engagement = engagement;
    }

    [HttpGet("curriculum")]
    public async Task<IActionResult> Curriculum()
    {
        var classes = await _context.LearningClasses
            .Where(learningClass => learningClass.IsPublished)
            .Include(learningClass => learningClass.Subjects)
            .OrderBy(learningClass => learningClass.DisplayOrder)
            .ToListAsync();
        return View(classes);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var model = new LearnIndexViewModel
        {
            Classes = await _context.LearningClasses
                .Where(learningClass => learningClass.IsPublished)
                .Include(learningClass => learningClass.Subjects)
                .OrderBy(learningClass => learningClass.DisplayOrder)
                .ToListAsync(),
            FeaturedLessons = await _context.Lessons
                .Where(lesson => lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished)
                .Include(lesson => lesson.Topic)
                    .ThenInclude(topic => topic.Chapter)
                .Include(lesson => lesson.ProtijogMapping)
                .OrderBy(lesson => lesson.DisplayOrder)
                .Take(6)
                .ToListAsync()
        };

        return View(model);
    }

    [HttpGet("class/{classSlug}")]
    public async Task<IActionResult> Class(string classSlug)
    {
        var learningClass = await _context.LearningClasses
            .Where(item => item.IsPublished && item.Slug == classSlug)
            .Include(item => item.Subjects.Where(subject => subject.IsPublished))
            .ThenInclude(subject => subject.Chapters.Where(chapter => chapter.IsPublished))
            .FirstOrDefaultAsync();

        return learningClass == null ? NotFound() : View(learningClass);
    }

    [HttpGet("class/{classSlug}/{subjectSlug}")]
    public async Task<IActionResult> Subject(string classSlug, string subjectSlug)
    {
        var subject = await _context.Subjects
            .Where(item => item.IsPublished && item.LearningClass.IsPublished && item.Slug == subjectSlug && item.LearningClass.Slug == classSlug)
            .Include(item => item.LearningClass)
            .Include(item => item.Chapters.Where(chapter => chapter.IsPublished))
                .ThenInclude(chapter => chapter.Topics.Where(topic => topic.IsPublished))
                    .ThenInclude(topic => topic.Lessons.Where(lesson => lesson.IsPublished))
            .FirstOrDefaultAsync();

        if (subject == null)
        {
            return NotFound();
        }

        return View(new SubjectLearningViewModel
        {
            Subject = subject,
            Chapters = subject.Chapters.OrderBy(chapter => chapter.DisplayOrder).ToList()
        });
    }

    [HttpGet("lesson/{id:int}")]
    public async Task<IActionResult> Lesson(int id)
    {
        var lesson = await _context.Lessons
            .Where(item => item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished && item.Id == id)
            .Include(item => item.Topic)
                .ThenInclude(topic => topic.Chapter)
                    .ThenInclude(chapter => chapter.Subject)
                        .ThenInclude(subject => subject.LearningClass)
            .Include(item => item.Video)
            .Include(item => item.ProtijogMapping)
            .FirstOrDefaultAsync();

        if (lesson == null)
        {
            return NotFound();
        }

        if (lesson.Video is { IsPublished: false }) lesson.Video = null;

        var progress = await FindProgressAsync(id);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSaved = userId != null && await _context.SavedLessons.AnyAsync(item => item.UserId == userId && item.LessonId == id);
        return View(new LessonDetailsViewModel
        {
            Lesson = lesson,
            ProgressPercent = progress?.PercentComplete ?? 0,
            IsCompleted = progress?.IsCompleted ?? false,
            IsSaved = isSaved,
            ResumePositionSeconds = progress?.LastPositionSeconds ?? 0,
            RelatedLessons = await _context.Lessons.AsNoTracking().Where(item => item.Id != id && item.TopicId == lesson.TopicId && item.IsPublished)
                .OrderBy(item => item.DisplayOrder).Take(4).ToListAsync()
        });
    }

    [Authorize]
    [HttpPost("lesson/{id:int}/progress")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TrackLessonProgress(int id, [FromBody] PlaybackProgressRequest request, CancellationToken cancellationToken)
    {
        if (request.Duration <= 0 || request.Position < 0 || !double.IsFinite(request.Position) || !double.IsFinite(request.Duration)) return BadRequest();
        var lessonExists = await _context.Lessons.AnyAsync(item => item.Id == id && item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished, cancellationToken);
        if (!lessonExists) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTime.UtcNow;
        var progress = await _context.LessonProgresses.FirstOrDefaultAsync(item => item.UserId == userId && item.LessonId == id, cancellationToken);
        if (progress == null)
        {
            progress = new LessonProgress { UserId = userId, LessonId = id, LastViewedAt = now };
            _context.LessonProgresses.Add(progress);
        }
        if (request.Position >= 5)
        {
            progress.StartedAtUtc ??= now;
            progress.LastPositionSeconds = Math.Min(request.Position, request.Duration);
            progress.PercentComplete = Math.Max(progress.PercentComplete, Math.Clamp((int)Math.Round(request.Position / request.Duration * 100), 0, 100));
            progress.LastViewedAt = now;
            if (progress.PercentComplete >= 90 && !progress.IsCompleted)
            {
                progress.IsCompleted = true;
                progress.CompletedAtUtc = now;
            }
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Json(new { progress.PercentComplete, progress.IsCompleted });
    }

    [Authorize]
    [HttpGet("/progress")]
    public async Task<IActionResult> Progress()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var totalLessons = await _context.Lessons.CountAsync(lesson => lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished);
        var completedLessons = await _context.LessonProgresses.CountAsync(progress => progress.UserId == userId && progress.IsCompleted && progress.Lesson.IsPublished && progress.Lesson.Topic.IsPublished && progress.Lesson.Topic.Chapter.IsPublished && progress.Lesson.Topic.Chapter.Subject.IsPublished && progress.Lesson.Topic.Chapter.Subject.LearningClass.IsPublished);
        var recentProgress = await _context.LessonProgresses
            .Where(progress => progress.UserId == userId && progress.Lesson.IsPublished && progress.Lesson.Topic.IsPublished && progress.Lesson.Topic.Chapter.IsPublished && progress.Lesson.Topic.Chapter.Subject.IsPublished && progress.Lesson.Topic.Chapter.Subject.LearningClass.IsPublished)
            .Include(progress => progress.Lesson)
                .ThenInclude(lesson => lesson.Topic)
            .OrderByDescending(progress => progress.LastViewedAt)
            .Take(12)
            .ToListAsync();
        var practiceMarks = await _context.QuestionAttempts.Where(item => item.UserId == userId)
            .SumAsync(item => (decimal?)item.Score) ?? 0;

        return View(new ProgressViewModel
        {
            TotalLessons = totalLessons,
            CompletedLessons = completedLessons,
            PracticeMarks = practiceMarks,
              Achievements = await _context.StudentAchievements.AsNoTracking()
                  .Where(item => item.UserId == userId)
                  .OrderByDescending(item => item.EarnedAtUtc)
                  .ToListAsync(),
            RecentProgress = recentProgress
        });
    }

    [Authorize]
    [HttpPost("lesson/{id:int}/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLesson(int id, string? returnUrl)
    {
        var lessonExists = await _context.Lessons.AnyAsync(lesson => lesson.Id == id && lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished);
        if (!lessonExists)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var lesson = await _context.Lessons.FirstOrDefaultAsync(item => item.Id == id);
        if (lesson?.VideoId != null)
        {
            var existing = await _context.LessonProgresses.FirstOrDefaultAsync(item => item.UserId == userId && item.LessonId == id);
            if (existing == null || existing.PercentComplete < 90)
            {
                TempData["Message"] = "Keep learning until you have watched most of the lesson to mark it complete.";
                return LocalRedirect(returnUrl ?? $"/learn/lesson/{id}");
            }
        }
        var progress = await _context.LessonProgresses
            .FirstOrDefaultAsync(item => item.UserId == userId && item.LessonId == id);

        if (progress == null)
        {
            progress = new LessonProgress { UserId = userId, LessonId = id };
            _context.LessonProgresses.Add(progress);
        }

        progress.PercentComplete = 100;
        progress.IsCompleted = true;
        progress.CompletedAtUtc ??= DateTime.UtcNow;
        progress.StartedAtUtc ??= DateTime.UtcNow;
        progress.LastViewedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        var completionCount = await _context.LessonProgresses.CountAsync(item => item.UserId == userId && item.IsCompleted);
        if (completionCount == 1) await _engagement.AwardAchievementAsync(userId, "first-lesson", "First lesson", "You finished your first lesson.", "🌱");
        if (completionCount >= 10) await _engagement.AwardAchievementAsync(userId, "lessons-10", "Ten lessons", "Ten lessons completed. Keep that momentum going.", "📚");

        TempData["Message"] = "Lesson marked complete. Keep going.";
        return LocalRedirect(returnUrl ?? $"/learn/lesson/{id}");
    }

    [HttpGet("/practice")]
    public async Task<IActionResult> Practice(CancellationToken cancellationToken)
    {
        ViewBag.PracticeLessons = await _context.Lessons.AsNoTracking()
            .Where(item => item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished)
            .Include(item => item.Topic).ThenInclude(topic => topic.Chapter).ThenInclude(chapter => chapter.Subject).ThenInclude(subject => subject.LearningClass)
            .OrderBy(item => item.Topic.Chapter.Subject.LearningClass.DisplayOrder).ThenBy(item => item.Topic.Chapter.Subject.DisplayOrder).ThenBy(item => item.DisplayOrder)
            .Take(60).ToListAsync(cancellationToken);
        return View();
    }

    [Authorize]
    [HttpGet("lesson/{id:int}/practice")]
    public async Task<IActionResult> LessonPractice(int id)
    {
        var lesson = await _context.Lessons
            .Where(item => item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished && item.Id == id)
            .Include(item => item.Topic)
                .ThenInclude(topic => topic.Chapter)
            .FirstOrDefaultAsync();

        if (lesson == null)
        {
            return NotFound();
        }

        var topic = await _context.Topics
            .Include(item => item.Chapter)
                .ThenInclude(chapter => chapter.Subject)
                    .ThenInclude(subject => subject.LearningClass)
            .FirstAsync(item => item.Id == lesson.TopicId);
        lesson.Topic = topic;

        var savedQuestions = await _context.Questions.AsNoTracking().Include(item => item.Lesson).ThenInclude(item => item.Topic).ThenInclude(item => item.Chapter).ThenInclude(item => item.Subject).ThenInclude(item => item.LearningClass).Where(item => item.LessonId == id && (item.Status == "Published" || item.Status == "AI Generated"))
            .OrderBy(item => item.Id).Take(10).ToListAsync();
        if (savedQuestions.Count == 0)
        {
            var timer = Stopwatch.StartNew();
            var generated = await _ai.GenerateQuestionsAsync(lesson);
            await _aiUsage.RecordAsync("Practice question generation", timer.ElapsedMilliseconds, generated.Count > 0, HttpContext.RequestAborted);
            foreach (var item in generated)
            {
                if (!PracticeQuestionViewModel.IsValid(item)) continue;
                var question = new Question
                {
                    LessonId = lesson.Id,
                    QuestionText = item.Prompt,
                    OptionsJson = System.Text.Json.JsonSerializer.Serialize(item.Options),
                    CorrectOption = item.CorrectOption,
                    ExpectedAnswer = item.ExpectedAnswer,
                    Explanation = item.Explanation,
                    QuestionType = item.QuestionType,
                    MaximumMarks = item.MaximumMarks,
                    AiProvider = "Gemini",
                    AiModel = "Configured AI provider",
                    Status = "AI Generated"
                };
                _context.Questions.Add(question);
                savedQuestions.Add(question);
            }
            if (savedQuestions.Count > 0) await _context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        var questions = savedQuestions.Select(question => new PracticeQuestionViewModel
        {
            Prompt = question.QuestionText,
            Options = System.Text.Json.JsonSerializer.Deserialize<string[]>(question.OptionsJson) ?? Array.Empty<string>(),
            CorrectOption = question.CorrectOption,
            Explanation = question.Explanation,
            QuestionId = question.Id,
            QuestionType = question.QuestionType,
            MaximumMarks = question.MaximumMarks,
            ExpectedAnswer = question.ExpectedAnswer
        }).ToList();
        if (questions.Count == 0)
            return View("PracticeLesson", new PracticeViewModel { Lesson = lesson });
        var sessionId = Guid.NewGuid().ToString("N");
        _cache.Set($"practice:{sessionId}", new StoredPractice(User.FindFirstValue(ClaimTypes.NameIdentifier)!, lesson.Id, questions), TimeSpan.FromMinutes(30));
        return View("PracticeLesson", new PracticeViewModel
        {
            Lesson = lesson,
            PracticeSessionId = sessionId,
            Questions = questions
        });
    }

    [Authorize]
    [HttpPost("practice/answer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitPracticeAnswer([FromBody] PracticeAnswerRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest();
        if (request.QuestionIndex < 0 || request.SelectedOption < -1 || request.Answer?.Length > 4000) return BadRequest();
        if (!_cache.TryGetValue($"practice:{request.SessionId}", out StoredPractice? stored) || stored is null ||
            stored.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return BadRequest(new { message = "This practice session expired. Start again." });
        if (request.LessonId != stored.LessonId) return BadRequest();
        if (request.QuestionIndex >= stored.Questions.Count) return BadRequest();
        var question = stored.Questions[request.QuestionIndex];
        var written = !PracticeQuestionViewModel.IsChoiceType(question.QuestionType);
        if (written ? string.IsNullOrWhiteSpace(request.Answer) : request.SelectedOption < 0 || request.SelectedOption >= question.Options.Length) return BadRequest();
        if (!stored.Answered.TryAdd(request.QuestionIndex, 0)) return BadRequest(new { message = "This answer has already been submitted." });
        var answerText = written ? request.Answer!.Trim() : question.Options[request.SelectedOption];
        decimal earned = written ? 0 : request.SelectedOption == question.CorrectOption ? 1 : 0;
        var isCorrect = false;
        var feedbackText = question.Explanation;
        if (written)
        {
            var savedQuestion = await _context.Questions.Include(item => item.Lesson).ThenInclude(item => item.Topic).ThenInclude(item => item.Chapter).ThenInclude(item => item.Subject).ThenInclude(item => item.LearningClass)
                .FirstOrDefaultAsync(item => item.Id == question.QuestionId && (item.Status == "Published" || item.Status == "AI Generated"), cancellationToken);
            if (savedQuestion == null) return BadRequest();
            var gradingTimer = Stopwatch.StartNew();
            var grade = await _ai.EvaluateAnswerAsync(savedQuestion, answerText, cancellationToken);
            await _aiUsage.RecordAsync("Answer grading", gradingTimer.ElapsedMilliseconds, !grade.Feedback.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase) && !grade.Feedback.Contains("could not grade", StringComparison.OrdinalIgnoreCase), cancellationToken);
            if (grade.Feedback.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase) || grade.Feedback.Contains("could not grade", StringComparison.OrdinalIgnoreCase))
            {
                stored.Answered.TryRemove(request.QuestionIndex, out _);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = grade.Feedback });
            }
            earned = grade.Score;
            feedbackText = grade.Feedback;
            isCorrect = earned >= savedQuestion.MaximumMarks;
        }
        else isCorrect = earned > 0;
        _context.QuestionAttempts.Add(new QuestionAttempt
        {
            UserId = stored.UserId,
            LessonId = request.LessonId,
            QuestionId = question.QuestionId,
            QuestionText = question.Prompt,
            StudentAnswer = answerText,
            IsCorrect = isCorrect,
            Score = earned,
            MaximumScore = question.MaximumMarks,
            Feedback = feedbackText,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        stored.AddScore(earned);
        if (isCorrect) Interlocked.Increment(ref stored.CorrectAnswers);
        var attempts = await _context.QuestionAttempts.CountAsync(item => item.UserId == stored.UserId, cancellationToken);
        if (attempts == 1) await _engagement.AwardAchievementAsync(stored.UserId, "first-practice", "First practice", "You completed your first practice answer.", "✨", cancellationToken);
        if (attempts >= 10) await _engagement.AwardAchievementAsync(stored.UserId, "practice-10", "Practice champion", "You have answered ten practice questions.", "🏅", cancellationToken);
        return Json(new { isCorrect, score = earned, maximumScore = question.MaximumMarks, feedback = feedbackText, explanation = question.Explanation, correctOption = question.CorrectOption });
    }

    [Authorize]
    [HttpPost("practice/result")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PracticeResult([FromBody] PracticeResultRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !_cache.TryGetValue($"practice:{request.SessionId}", out StoredPractice? stored) || stored is null ||
            stored.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier)) return BadRequest(new { message = "This practice session expired. Start again." });
        var attempted = stored.Answered.Count;
        if (attempted != stored.Questions.Count) return BadRequest(new { message = "Answer each question before viewing your result." });
        var percent = stored.MaximumMarks == 0 ? 0 : (int)Math.Round((double)(stored.EarnedMarks / stored.MaximumMarks) * 100);
        if (stored.TryMarkCompletionNotified())
        {
            var lifetimeMarks = await _context.QuestionAttempts.Where(item => item.UserId == stored.UserId).SumAsync(item => (decimal?)item.Score, cancellationToken) ?? 0;
            await _engagement.NotifyPracticeCompleteAsync(stored.UserId, stored.EarnedMarks, stored.MaximumMarks, lifetimeMarks, cancellationToken);
        }
        return Json(new { score = stored.EarnedMarks, maximumScore = stored.MaximumMarks, attempted, percent });
    }

    private sealed class StoredPractice
    {
        private readonly object _scoreLock = new();
        private decimal _earnedMarks;
        public string UserId { get; }
        public int LessonId { get; }
        public IReadOnlyList<PracticeQuestionViewModel> Questions { get; }
        public System.Collections.Concurrent.ConcurrentDictionary<int, byte> Answered { get; } = new();
        public int CorrectAnswers;
        private int _completionNotified;
        public bool TryMarkCompletionNotified() => Interlocked.Exchange(ref _completionNotified, 1) == 0;
        public decimal EarnedMarks { get { lock (_scoreLock) return _earnedMarks; } }
        public decimal MaximumMarks { get; }
        public StoredPractice(string userId, int lessonId, IReadOnlyList<PracticeQuestionViewModel> questions)
        {
            UserId = userId; LessonId = lessonId; Questions = questions;
            MaximumMarks = questions.Sum(item => item.MaximumMarks);
        }
        public void AddScore(decimal marks) { lock (_scoreLock) _earnedMarks += marks; }
    }
    public sealed record PracticeAnswerRequest(string SessionId, int LessonId, int QuestionIndex, int SelectedOption, string? Answer);
    public sealed record PracticeResultRequest(string SessionId);
    public sealed record PlaybackProgressRequest(double Position, double Duration);

    private async Task<LessonProgress?> FindProgressAsync(int lessonId)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _context.LessonProgresses
            .FirstOrDefaultAsync(item => item.UserId == userId && item.LessonId == lessonId);
    }

}
