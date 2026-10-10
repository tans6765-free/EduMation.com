using EduMation.Data;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

public class SearchController : Controller
{
    private readonly ApplicationDbContext _context;
    public SearchController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(string? query, CancellationToken cancellationToken)
    {
        query = query?.Trim();
        var model = new SearchViewModel { Query = query ?? string.Empty };
        if (!string.IsNullOrWhiteSpace(query))
        {
            var match = $"%{query}%";
            model.Lessons = await _context.Lessons.AsNoTracking()
                .Where(lesson => lesson.IsPublished && lesson.Topic.IsPublished && lesson.Topic.Chapter.IsPublished && lesson.Topic.Chapter.Subject.IsPublished && lesson.Topic.Chapter.Subject.LearningClass.IsPublished &&
                    (EF.Functions.Like(lesson.Title, match) || EF.Functions.Like(lesson.Description, match) ||
                     EF.Functions.Like(lesson.Topic.Title, match) || EF.Functions.Like(lesson.Topic.Chapter.Title, match) ||
                     EF.Functions.Like(lesson.Topic.Chapter.Subject.Name, match) || EF.Functions.Like(lesson.Topic.Chapter.Subject.LearningClass.Name, match)))
                .Include(lesson => lesson.Topic).ThenInclude(topic => topic.Chapter)
                    .ThenInclude(chapter => chapter.Subject).ThenInclude(subject => subject.LearningClass)
                .OrderBy(lesson => lesson.Topic.Chapter.Subject.LearningClass.DisplayOrder)
                .ThenBy(lesson => lesson.Topic.Chapter.Subject.DisplayOrder)
                .ThenBy(lesson => lesson.DisplayOrder)
                .Take(30).ToListAsync(cancellationToken);
            model.Videos = await _context.Videos.AsNoTracking()
                .Where(video => video.IsPublished && (EF.Functions.Like(video.Title, match) || EF.Functions.Like(video.Genre, match) || EF.Functions.Like(video.Description, match)))
                .OrderByDescending(video => video.UploadDate).Take(30).ToListAsync(cancellationToken);
        }
        return View(model);
    }
}
