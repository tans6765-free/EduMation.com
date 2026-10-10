using EduMation.Models;

namespace EduMation.Services;

public interface IGoogleCalendarService
{
    Task<string?> UpsertEventAsync(string userId, CalendarReminder reminder, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(string userId, string eventId, CancellationToken cancellationToken = default);
}
