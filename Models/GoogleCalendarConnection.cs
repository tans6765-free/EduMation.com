using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class GoogleCalendarConnection
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
    [Required] public string ProtectedRefreshToken { get; set; } = string.Empty;
    [StringLength(255)] public string CalendarId { get; set; } = "primary";
    public DateTimeOffset ConnectedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
