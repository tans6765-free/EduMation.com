using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class StudentAchievement
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(300)] public string Description { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Icon { get; set; } = "sparkles";
    public DateTimeOffset EarnedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
