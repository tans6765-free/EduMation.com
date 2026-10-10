using System.Globalization;
using System.Security.Claims;
using System.Text;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/students")]
public sealed class AdminStudentsController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminStudentsViewModel filters, CancellationToken cancellationToken)
    {
        await PopulateAsync(filters, cancellationToken);
        return View(filters);
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] AdminStudentsViewModel filters, CancellationToken cancellationToken)
    {
        await PopulateAsync(filters, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("User ID,Full name,Email,Class,District,Contact number,Registration date,Progress percent,Badges");
        foreach (var student in filters.Students)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(student.UserId), Csv(student.FullName), Csv(student.Email), Csv(student.ClassName), Csv(student.District),
                Csv(student.ContactNumber), Csv(student.RegisteredAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                Csv(student.ProgressPercent.ToString(CultureInfo.InvariantCulture)), Csv(string.Join("; ", student.Badges))
            }));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"edumation-students-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task PopulateAsync(AdminStudentsViewModel model, CancellationToken cancellationToken)
    {
        model.Classes = await db.LearningClasses.AsNoTracking().OrderBy(item => item.DisplayOrder).ToListAsync(cancellationToken);
        model.Badges = await db.StudentAchievements.AsNoTracking().GroupBy(item => item.Code)
            .Select(group => new BadgeFilterOption(group.Key, group.Select(item => item.Title).First())).OrderBy(item => item.Title).ToListAsync(cancellationToken);

        var studentIds = db.UserRoles.Join(db.Roles, link => link.RoleId, role => role.Id, (link, role) => new { link.UserId, role.Name })
            .Where(item => item.Name == "Student").Select(item => item.UserId);
        var roster = await (from user in db.Users.AsNoTracking()
            join profile in db.Profiles.AsNoTracking() on user.Id equals profile.UserId
            where studentIds.Contains(user.Id)
            select new
            {
                user.Id, user.UserName, user.Email, user.RegisteredAtUtc,
                profile.FirstName, profile.LastName, profile.District, profile.ContactNumber,
                ClassName = profile.LearningClass == null ? "Not selected" : profile.LearningClass.Name,
                profile.LearningClassId
            }).ToListAsync(cancellationToken);
        var userIds = roster.Select(item => item.Id).ToArray();
        var completed = await db.LessonProgresses.AsNoTracking().Where(item => userIds.Contains(item.UserId) && item.IsCompleted)
            .GroupBy(item => item.UserId).Select(group => new { UserId = group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);
        var totalLessons = await db.Lessons.CountAsync(item => item.IsPublished && item.Topic.IsPublished && item.Topic.Chapter.IsPublished && item.Topic.Chapter.Subject.IsPublished && item.Topic.Chapter.Subject.LearningClass.IsPublished, cancellationToken);
        var earnedBadges = await db.StudentAchievements.AsNoTracking().Where(item => userIds.Contains(item.UserId))
            .Select(item => new { item.UserId, item.Code }).ToListAsync(cancellationToken);
        var badgesByUser = earnedBadges.GroupBy(item => item.UserId).ToDictionary(group => group.Key, group => group.Select(item => item.Code).ToList());

        var rows = roster.Select(item => new AdminStudentRow(item.UserName ?? item.Id,
            $"{item.FirstName} {item.LastName}".Trim(), item.Email ?? string.Empty, item.District, item.ContactNumber,
            item.ClassName, item.LearningClassId, item.RegisteredAtUtc,
            totalLessons == 0 ? 0 : (int)Math.Round(completed.GetValueOrDefault(item.Id) * 100d / totalLessons),
            badgesByUser.TryGetValue(item.Id, out var earned) ? earned : Array.Empty<string>()))
            .Where(item => !model.LearningClassId.HasValue || item.LearningClassId == model.LearningClassId)
            .Where(item => !model.RegisteredFrom.HasValue || item.RegisteredAtUtc >= new DateTimeOffset(model.RegisteredFrom.Value.Date, TimeSpan.Zero))
            .Where(item => !model.RegisteredTo.HasValue || item.RegisteredAtUtc < new DateTimeOffset(model.RegisteredTo.Value.Date.AddDays(1), TimeSpan.Zero))
            .Where(item => !model.MinimumProgress.HasValue || item.ProgressPercent >= model.MinimumProgress)
            .Where(item => !model.MaximumProgress.HasValue || item.ProgressPercent <= model.MaximumProgress)
            .Where(item => string.IsNullOrWhiteSpace(model.BadgeCode) || item.Badges.Contains(model.BadgeCode, StringComparer.Ordinal))
            .OrderBy(item => item.ClassName).ThenBy(item => item.FullName).ToList();
        model.Students = rows;
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
