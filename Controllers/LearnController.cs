using System.Security.Claims;
using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Route("learn")]
public class LearnController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAiLearningService _aiLearningService;

    public LearnController(ApplicationDbContext context, IAiLearningService aiLearningService)
    {
        _context = context;
        _aiLearningService = aiLearningService;
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
                .Where(lesson => lesson.IsPublished)
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
            .Where(item => item.IsPublished && item.Slug == subjectSlug && item.LearningClass.Slug == classSlug)
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
            .Where(item => item.IsPublished && item.Id == id)
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

        var progress = await FindProgressAsync(id);
        return View(new LessonDetailsViewModel
        {
            Lesson = lesson,
            ProgressPercent = progress?.PercentComplete ?? 0,
            IsCompleted = progress?.IsCompleted ?? false
        });
    }

    [Authorize]
    [HttpGet("/progress")]
    public async Task<IActionResult> Progress()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var totalLessons = await _context.Lessons.CountAsync(lesson => lesson.IsPublished);
        var recentProgress = await _context.LessonProgresses
            .Where(progress => progress.UserId == userId)
            .Include(progress => progress.Lesson)
                .ThenInclude(lesson => lesson.Topic)
            .OrderByDescending(progress => progress.LastViewedAt)
            .Take(12)
            .ToListAsync();

        return View(new ProgressViewModel
        {
            TotalLessons = totalLessons,
            CompletedLessons = recentProgress.Count(progress => progress.IsCompleted),
            RecentProgress = recentProgress
        });
    }

    [Authorize]
    [HttpPost("lesson/{id:int}/complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLesson(int id, string? returnUrl)
    {
        var lessonExists = await _context.Lessons.AnyAsync(lesson => lesson.Id == id && lesson.IsPublished);
        if (!lessonExists)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var progress = await _context.LessonProgresses
            .FirstOrDefaultAsync(item => item.UserId == userId && item.LessonId == id);

        if (progress == null)
        {
            progress = new LessonProgress { UserId = userId, LessonId = id };
            _context.LessonProgresses.Add(progress);
        }

        progress.PercentComplete = 100;
        progress.IsCompleted = true;
        progress.LastViewedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Message"] = "Lesson marked complete. Keep going.";
        return LocalRedirect(returnUrl ?? $"/learn/lesson/{id}");
    }

    [HttpGet("/practice")]
    public IActionResult Practice()
    {
        return View();
    }

    [HttpGet("lesson/{id:int}/practice")]
    public async Task<IActionResult> LessonPractice(int id)
    {
        var lesson = await _context.Lessons
            .Where(item => item.IsPublished && item.Id == id)
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

        return View("PracticeLesson", new PracticeViewModel
        {
            Lesson = lesson,
            Questions = await _aiLearningService.GenerateQuestionsAsync(lesson)
        });
    }

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
