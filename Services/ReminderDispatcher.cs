using EduMation.Data;
using EduMation.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Services;

public sealed class ReminderDispatcher(IServiceScopeFactory scopeFactory, ILogger<ReminderDispatcher> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await DispatchDueReminders(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Reminder dispatch failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchDueReminders(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var now = DateTimeOffset.UtcNow;
        var due = await db.CalendarReminders.Where(item => item.StartsAt.AddMinutes(-item.ReminderMinutes) <= now && item.StartsAt > now.AddDays(-1) &&
            (item.NotifiedAtUtc == null || item.EmailNotification && item.EmailSentAtUtc == null && item.EmailAttempts < 8 && (item.LastEmailAttemptAtUtc == null || item.LastEmailAttemptAtUtc <= now.AddMinutes(-10))))
            .OrderBy(item => item.StartsAt).Take(100).ToListAsync(cancellationToken);
        foreach (var reminder in due)
        {
            var student = await db.Users.Where(user => user.Id == reminder.UserId).Select(user => new { user.Email, user.EmailConfirmed }).FirstOrDefaultAsync(cancellationToken);
            if (student == null) continue;
            var zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Calendar:TimeZone"] ?? "Asia/Dhaka");
            var localStart = TimeZoneInfo.ConvertTime(reminder.StartsAt, zone);
            var message = reminder.StartsAt <= now ? $"{reminder.Title} is happening now." : $"{reminder.Title} is coming up {localStart:ddd, MMM d 'at' h:mm tt} (Dhaka time).";
            if (reminder.NotifiedAtUtc == null)
            {
                db.StudentNotifications.Add(new StudentNotification { UserId = reminder.UserId, Title = "Calendar reminder", Message = message, Link = "/calendar", BrowserNotification = reminder.BrowserNotification });
                reminder.NotifiedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
            }
            if (reminder.EmailNotification && reminder.EmailSentAtUtc == null && student.EmailConfirmed && !string.IsNullOrWhiteSpace(student.Email))
            {
                reminder.EmailAttempts++;
                reminder.LastEmailAttemptAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                try
                {
                    var baseUrl = configuration["App:PublicBaseUrl"]?.TrimEnd('/');
                    var calendarUrl = string.IsNullOrWhiteSpace(baseUrl) ? "Sign in to EduMation and open My Calendar." : $"Open your EduMation calendar: {baseUrl}/calendar";
                    await email.SendAsync(student.Email, $"EduMation reminder: {reminder.Title}", $"{message}\n\n{calendarUrl}", cancellationToken);
                    reminder.EmailSentAtUtc = now;
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception) { logger.LogWarning(exception, "Could not send a calendar reminder email."); }
            }
        }
    }
}
