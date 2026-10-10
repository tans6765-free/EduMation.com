using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class CalendarReminder
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(140)] public string Title { get; set; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; set; }
    [Required, StringLength(30)] public string Category { get; set; } = "Exam";
    public DateTimeOffset StartsAt { get; set; }
    public int ReminderMinutes { get; set; } = 1440;
    public bool EmailNotification { get; set; } = true;
    public bool BrowserNotification { get; set; } = true;
    public bool IsAllDay { get; set; }
    public DateTimeOffset? NotifiedAtUtc { get; set; }
    public DateTimeOffset? EmailSentAtUtc { get; set; }
    public DateTimeOffset? LastEmailAttemptAtUtc { get; set; }
    public int EmailAttempts { get; set; }
    [StringLength(255)] public string? GoogleEventId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
