namespace EduMation.Models;

public class LessonVersion
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string LearningObjectives { get; set; } = string.Empty;
    public string KeyConcepts { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
