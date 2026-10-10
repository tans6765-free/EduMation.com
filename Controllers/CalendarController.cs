using System.Security.Claims;
using EduMation.Data;
using EduMation.Models;
using EduMation.ViewModel;
using EduMation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;

namespace EduMation.Controllers;

[Authorize]
[Route("calendar")]
public sealed class CalendarController(ApplicationDbContext db, IGoogleCalendarService google, IDataProtectionProvider protection, IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<CalendarController> logger, IStudentEngagementService engagement) : Controller
{
    private readonly IDataProtector _tokenProtector = protection.CreateProtector("EduMation.GoogleCalendar.RefreshToken.v1");
    [HttpGet("")]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var dhakaNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GetCalendarTimeZone());
        var selectedMonth = new DateOnly(year ?? dhakaNow.Year, month ?? dhakaNow.Month, 1);
        var from = new DateTimeOffset(selectedMonth.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = from.AddMonths(1);
        var reminders = await db.CalendarReminders.AsNoTracking().Where(item => item.UserId == userId && item.StartsAt >= from && item.StartsAt < to).OrderBy(item => item.StartsAt).ToListAsync();
        var achievements = await db.StudentAchievements.AsNoTracking().Where(item => item.UserId == userId).OrderByDescending(item => item.EarnedAtUtc).ToListAsync();
        var activityTimes = await db.QuestionAttempts.Where(item => item.UserId == userId).Select(item => item.CreatedAtUtc)
            .Union(db.LessonProgresses.Where(item => item.UserId == userId && item.IsCompleted && item.CompletedAtUtc.HasValue).Select(item => item.CompletedAtUtc!.Value)).Distinct().ToListAsync();
        var dates = activityTimes.Select(time => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)), GetCalendarTimeZone()).DateTime)).ToHashSet();
        var cursor = DateOnly.FromDateTime(dhakaNow.DateTime);
        if (!dates.Contains(cursor)) cursor = cursor.AddDays(-1);
        var streak = 0;
        while (dates.Contains(cursor)) { streak++; cursor = cursor.AddDays(-1); }
        if (streak >= 3) await engagement.AwardAchievementAsync(userId, "streak-3", "Three-day streak", "You learned three days in a row.", "🔥");
        if (streak >= 7) await engagement.AwardAchievementAsync(userId, "streak-7", "Week of learning", "You learned seven days in a row.", "🌟");
        if (!achievements.Any(item => item.Code == "first-practice") && await db.QuestionAttempts.AnyAsync(item => item.UserId == userId))
            await engagement.AwardAchievementAsync(userId, "first-practice", "First practice", "You completed your first practice answer.", "✨");
        return View(new CalendarViewModel { Month = selectedMonth, Reminders = reminders, Achievements = await db.StudentAchievements.AsNoTracking().Where(item => item.UserId == userId).OrderByDescending(item => item.EarnedAtUtc).ToListAsync(), CurrentStreak = streak, GoogleCalendarConnected = await db.GoogleCalendarConnections.AnyAsync(item => item.UserId == userId) });
    }

    [HttpPost("save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CalendarReminderInput input)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var parsedLocal = DateTime.TryParseExact(input.StartsAtLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDateTime);
        var startsAt = parsedLocal ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), GetCalendarTimeZone()), TimeSpan.Zero) : DateTimeOffset.MinValue;
        if (input.Category is not ("Exam" or "Assignment" or "Study" or "Personal" or "Other") || !parsedLocal || startsAt < DateTimeOffset.UtcNow.AddMinutes(-1))
            ModelState.AddModelError(string.Empty, "Choose a reminder type and a future date and time.");
        if (!ModelState.IsValid) return RedirectToAction(nameof(Index), new { year = parsedLocal ? localDateTime.Year : (int?)null, month = parsedLocal ? localDateTime.Month : (int?)null });
        CalendarReminder? reminder = null;
        if (input.Id.HasValue)
            reminder = await db.CalendarReminders.FirstOrDefaultAsync(item => item.Id == input.Id && item.UserId == userId);
        if (reminder == null && input.Id.HasValue) return NotFound();
        reminder ??= new CalendarReminder { UserId = userId };
        reminder.Title = input.Title.Trim(); reminder.Notes = input.Notes?.Trim(); reminder.Category = input.Category;
        reminder.StartsAt = startsAt; reminder.ReminderMinutes = input.ReminderMinutes;
        reminder.EmailNotification = input.EmailNotification; reminder.BrowserNotification = input.BrowserNotification; reminder.IsAllDay = input.IsAllDay;
        reminder.NotifiedAtUtc = null; reminder.EmailSentAtUtc = null;
        if (!db.CalendarReminders.Local.Contains(reminder)) db.CalendarReminders.Add(reminder);
        await db.SaveChangesAsync();
        try
        {
            reminder.GoogleEventId = await google.UpsertEventAsync(userId, reminder);
            await db.SaveChangesAsync();
            TempData["Message"] = reminder.GoogleEventId == null ? "Calendar reminder saved." : "Calendar reminder saved and synced to Google Calendar.";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Google Calendar sync failed for a reminder.");
            TempData["Message"] = "Reminder saved in EduMation. Google Calendar sync could not complete; reconnect your account to retry.";
        }
        return RedirectToAction(nameof(Index), new { year = localDateTime.Year, month = localDateTime.Month });
    }

    [HttpPost("{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var reminder = await db.CalendarReminders.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
        if (reminder == null) return NotFound();
        if (reminder.GoogleEventId != null)
        {
            try { await google.DeleteEventAsync(userId, reminder.GoogleEventId); }
            catch (Exception exception) { logger.LogWarning(exception, "Could not remove Google Calendar event while deleting reminder."); }
        }
        db.CalendarReminders.Remove(reminder);
        await db.SaveChangesAsync();
        TempData["Message"] = "Reminder deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("google/connect")]
    public IActionResult ConnectGoogle()
    {
        var clientId = configuration["GoogleCalendar:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(configuration["GoogleCalendar:ClientSecret"]))
        {
            TempData["Message"] = "Google Calendar sync needs OAuth Client ID and Client Secret. EduMation reminders still work.";
            return RedirectToAction(nameof(Index));
        }
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Response.Cookies.Append("edumation_google_state", state, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, MaxAge = TimeSpan.FromMinutes(10), Path = "/calendar/google" });
        var redirectUri = GetGoogleRedirectUri();
        var url = "https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["redirect_uri"] = redirectUri, ["response_type"] = "code", ["scope"] = "https://www.googleapis.com/auth/calendar.events",
            ["access_type"] = "offline", ["prompt"] = "consent", ["include_granted_scopes"] = "true", ["state"] = state
        }.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return Redirect(url);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(string? code, string? state, string? error, CancellationToken cancellationToken)
    {
        var expectedState = Request.Cookies["edumation_google_state"];
        Response.Cookies.Delete("edumation_google_state", new CookieOptions { Path = "/calendar/google", Secure = true, SameSite = SameSiteMode.Lax });
        if (string.IsNullOrWhiteSpace(expectedState) || string.IsNullOrWhiteSpace(state) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(expectedState), System.Text.Encoding.UTF8.GetBytes(state))) return BadRequest("Google authorization state is invalid. Please reconnect and try again.");
        if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code)) { TempData["Message"] = "Google Calendar connection was cancelled."; return RedirectToAction(nameof(Index)); }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        using var response = await httpFactory.CreateClient().PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code, ["client_id"] = configuration["GoogleCalendar:ClientId"] ?? "", ["client_secret"] = configuration["GoogleCalendar:ClientSecret"] ?? "",
            ["redirect_uri"] = GetGoogleRedirectUri(), ["grant_type"] = "authorization_code"
        }), cancellationToken);
        if (!response.IsSuccessStatusCode) { logger.LogWarning("Google OAuth token exchange failed with status {StatusCode}.", response.StatusCode); TempData["Message"] = "Google Calendar authorization could not be completed."; return RedirectToAction(nameof(Index)); }
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var refreshToken = payload.RootElement.TryGetProperty("refresh_token", out var tokenValue) ? tokenValue.GetString() : null;
        var existing = await db.GoogleCalendarConnections.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            if (existing == null) db.GoogleCalendarConnections.Add(new GoogleCalendarConnection { UserId = userId, ProtectedRefreshToken = _tokenProtector.Protect(refreshToken) });
            else { existing.ProtectedRefreshToken = _tokenProtector.Protect(refreshToken); existing.ConnectedAtUtc = DateTimeOffset.UtcNow; }
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (existing == null) { TempData["Message"] = "Google did not provide offline access. Revoke EduMation in your Google account and try connecting again."; return RedirectToAction(nameof(Index)); }
        var unsynced = await db.CalendarReminders.Where(item => item.UserId == userId && item.GoogleEventId == null).ToListAsync(cancellationToken);
        foreach (var reminder in unsynced)
        {
            try { reminder.GoogleEventId = await google.UpsertEventAsync(userId, reminder, cancellationToken); }
            catch (Exception exception) { logger.LogWarning(exception, "Could not initially sync a student reminder to Google Calendar."); }
        }
        await db.SaveChangesAsync(cancellationToken);
        TempData["Message"] = "Google Calendar connected. New reminders will sync automatically.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("google/disconnect"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectGoogle(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var connection = await db.GoogleCalendarConnections.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (connection != null)
        {
            try
            {
                var refreshToken = _tokenProtector.Unprotect(connection.ProtectedRefreshToken);
                using var revoke = await httpFactory.CreateClient().PostAsync("https://oauth2.googleapis.com/revoke", new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken }), cancellationToken);
            }
            catch (Exception exception) { logger.LogWarning(exception, "Google token revocation did not complete during disconnect."); }
            db.GoogleCalendarConnections.Remove(connection);
            await db.SaveChangesAsync(cancellationToken);
        }
        TempData["Message"] = "Google Calendar disconnected. Your EduMation reminders are still here.";
        return RedirectToAction(nameof(Index));
    }

    private string GetGoogleRedirectUri() => configuration["GoogleCalendar:RedirectUri"] ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/calendar/google/callback";

    private TimeZoneInfo GetCalendarTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(configuration["Calendar:TimeZone"] ?? "Asia/Dhaka");

}
