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

    [StringLength(800)]
    public string LearningObjectives { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public int? VideoId { get; set; }
    public Video? Video { get; set; }
    public ProtijogMapping? ProtijogMapping { get; set; }
    public ICollection<LessonProgress> Progress { get; set; } = new List<LessonProgress>();
}
