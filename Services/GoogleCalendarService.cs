using System.Net.Http.Json;
using System.Text.Json;
using EduMation.Data;
using EduMation.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Services;

public sealed class GoogleCalendarService(HttpClient http, ApplicationDbContext db, IDataProtectionProvider protection, IConfiguration configuration) : IGoogleCalendarService
{
    private readonly IDataProtector _protector = protection.CreateProtector("EduMation.GoogleCalendar.RefreshToken.v1");

    public async Task<string?> UpsertEventAsync(string userId, CalendarReminder reminder, CancellationToken cancellationToken = default)
    {
        var connection = await db.GoogleCalendarConnections.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (connection == null) return null;
        var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
        using var request = new HttpRequestMessage(reminder.GoogleEventId == null ? HttpMethod.Post : HttpMethod.Patch,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(connection.CalendarId)}/events" + (reminder.GoogleEventId == null ? "" : "/" + Uri.EscapeDataString(reminder.GoogleEventId)));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(BuildGoogleEvent(reminder));
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.TryGetProperty("id", out var id) ? id.GetString() : reminder.GoogleEventId;
    }

    public async Task DeleteEventAsync(string userId, string eventId, CancellationToken cancellationToken = default)
    {
        var connection = await db.GoogleCalendarConnections.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (connection == null) return;
        var token = await GetAccessTokenAsync(connection, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(connection.CalendarId)}/events/{Uri.EscapeDataString(eventId)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
    }

    private async Task<string> GetAccessTokenAsync(GoogleCalendarConnection connection, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>
        {
            ["client_id"] = configuration["GoogleCalendar:ClientId"] ?? throw new InvalidOperationException("Google Calendar OAuth is not configured."),
            ["client_secret"] = configuration["GoogleCalendar:ClientSecret"] ?? throw new InvalidOperationException("Google Calendar OAuth is not configured."),
            ["refresh_token"] = _protector.Unprotect(connection.ProtectedRefreshToken),
            ["grant_type"] = "refresh_token"
        };
        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return body.RootElement.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Google did not return an access token.");
    }

    private object BuildGoogleEvent(CalendarReminder reminder)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(configuration["Calendar:TimeZone"] ?? "Asia/Dhaka");
        var localStart = TimeZoneInfo.ConvertTime(reminder.StartsAt, zone);
        var body = new Dictionary<string, object?> { ["summary"] = reminder.Title, ["description"] = reminder.Notes, ["colorId"] = reminder.Category switch { "Exam" => "11", "Assignment" => "5", "Study" => "10", _ => "9" } };
        if (reminder.IsAllDay)
        {
            var date = localStart.ToString("yyyy-MM-dd");
            body["start"] = new { date };
            body["end"] = new { date = localStart.AddDays(1).ToString("yyyy-MM-dd") };
        }
        else
        {
            body["start"] = new { dateTime = reminder.StartsAt.ToString("O") };
            body["end"] = new { dateTime = reminder.StartsAt.AddMinutes(60).ToString("O") };
        }
        body["reminders"] = new { useDefault = false, overrides = reminder.ReminderMinutes == 0 ? Array.Empty<object>() : new object[] { new { method = "popup", minutes = reminder.ReminderMinutes } } };
        return body;
    }
}
