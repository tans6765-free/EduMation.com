using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class LearningClass
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Slug { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}
