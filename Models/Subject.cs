using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class Subject
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public int LearningClassId { get; set; }
    public LearningClass LearningClass { get; set; } = null!;
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}
