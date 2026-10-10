using System.ComponentModel.DataAnnotations;
using EduMation.Models;

namespace EduMation.ViewModel;

public class CalendarViewModel
{
    public DateOnly Month { get; set; }
    public IReadOnlyList<CalendarReminder> Reminders { get; set; } = Array.Empty<CalendarReminder>();
    public IReadOnlyList<StudentAchievement> Achievements { get; set; } = Array.Empty<StudentAchievement>();
    public int CurrentStreak { get; set; }
    public bool GoogleCalendarConnected { get; set; }
}

public class CalendarReminderInput
{
    public int? Id { get; set; }
    [Required, StringLength(140)] public string Title { get; set; } = string.Empty;
    [StringLength(2000)] public string? Notes { get; set; }
    [Required] public string Category { get; set; } = "Exam";
    [Required] public string StartsAtLocal { get; set; } = string.Empty;
    [Range(0, 10080)] public int ReminderMinutes { get; set; } = 1440;
    public bool EmailNotification { get; set; } = true;
    public bool BrowserNotification { get; set; } = true;
    public bool IsAllDay { get; set; }
}
