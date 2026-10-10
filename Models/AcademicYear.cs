using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class AcademicYear
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Name { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsCurrent { get; set; }
    public ICollection<LearningClass> Classes { get; set; } = new List<LearningClass>();
}
