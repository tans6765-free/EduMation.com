using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class AiUsageLog
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Feature { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Provider { get; set; } = string.Empty;
    [StringLength(120)] public string Model { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Outcome { get; set; } = "Success";
    public long DurationMs { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
