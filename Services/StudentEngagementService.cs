using EduMation.Data;
using EduMation.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Services;

public sealed class StudentEngagementService(ApplicationDbContext db, IEmailSender email, ILogger<StudentEngagementService> logger) : IStudentEngagementService
{
    public async Task AwardAchievementAsync(string userId, string code, string title, string description, string icon, CancellationToken cancellationToken = default)
    {
        if (await db.StudentAchievements.AnyAsync(item => item.UserId == userId && item.Code == code, cancellationToken)) return;
        db.StudentAchievements.Add(new StudentAchievement { UserId = userId, Code = code, Title = title, Description = description, Icon = icon });
        db.StudentNotifications.Add(new StudentNotification
        {
            UserId = userId, Title = $"Badge earned: {title}", Message = description, Link = "/calendar", BrowserNotification = true
        });
        await db.SaveChangesAsync(cancellationToken);
        await TryEmailAsync(userId, $"You earned the {title} badge!", $"{description}\n\nSee your achievements in EduMation.", cancellationToken);
    }

    public async Task NotifyPracticeCompleteAsync(string userId, decimal score, decimal maximumScore, decimal lifetimeMarks, CancellationToken cancellationToken = default)
    {
        if (maximumScore <= 0) return;
        var previousLevel = GetLevel(Math.Max(0, lifetimeMarks - score));
        var currentLevel = GetLevel(lifetimeMarks);
        var percentage = Math.Round(score * 100 / maximumScore, 1);
        var resultMessage = $"You scored {score:0.##} out of {maximumScore:0.##} ({percentage:0.#}%) in your latest practice.";
        db.StudentNotifications.Add(new StudentNotification { UserId = userId, Title = "Practice score", Message = resultMessage, Link = "/learn/progress", BrowserNotification = true });
        string? levelMessage = null;
        if (currentLevel > previousLevel)
        {
            levelMessage = $"You reached Level {currentLevel}! Keep learning to reach the next level.";
            db.StudentNotifications.Add(new StudentNotification { UserId = userId, Title = $"Level up: Level {currentLevel}", Message = levelMessage, Link = "/learn/progress", BrowserNotification = true });
        }
        await db.SaveChangesAsync(cancellationToken);
        var body = levelMessage == null ? resultMessage : $"{resultMessage}\n\n{levelMessage}";
        await TryEmailAsync(userId, levelMessage == null ? "Your EduMation practice score" : $"Your EduMation score and Level {currentLevel}", body, cancellationToken);
    }

    private static int GetLevel(decimal marks) => 1 + (int)Math.Floor(Math.Max(0, marks) / 50m);

    private async Task TryEmailAsync(string userId, string subject, string body, CancellationToken cancellationToken)
    {
        var account = await db.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.EmailConfirmed }).FirstOrDefaultAsync(cancellationToken);
        if (account is not { EmailConfirmed: true } || string.IsNullOrWhiteSpace(account.Email)) return;
        try { await email.SendAsync(account.Email, subject, body, cancellationToken); }
        catch (Exception exception) { logger.LogWarning(exception, "Student achievement or score email delivery failed."); }
    }
}
