using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class LessonProgress
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public int PercentComplete { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime LastViewedAt { get; set; }
}
