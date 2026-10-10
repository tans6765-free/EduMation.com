using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class SavedLesson
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string UserId { get; set; } = string.Empty;
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
