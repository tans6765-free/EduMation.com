using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class ProtijogMapping
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    [Required, StringLength(120)]
    public string PracticeType { get; set; } = "Topic";

    [Required, StringLength(200)]
    public string Title { get; set; } = "Practice on Protijog";

    [Required, StringLength(500)]
    public string ExternalPath { get; set; } = "/protijog-coming-soon";

    public bool IsActive { get; set; } = true;
}
