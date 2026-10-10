using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class Lesson
{
    public int Id { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(180)]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(1200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(4000)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(800)]
    public string LearningObjectives { get; set; } = string.Empty;

    [StringLength(4000)]
    public string KeyConcepts { get; set; } = string.Empty;

    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public int? VideoId { get; set; }
    public Video? Video { get; set; }
    public ProtijogMapping? ProtijogMapping { get; set; }
    public ICollection<LessonProgress> Progress { get; set; } = new List<LessonProgress>();
    public ICollection<LessonVersion> Versions { get; set; } = new List<LessonVersion>();
}
