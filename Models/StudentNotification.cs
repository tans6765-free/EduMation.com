using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class StudentNotification
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Message { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Link { get; set; } = "/calendar";
    public bool BrowserNotification { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAtUtc { get; set; }
}
