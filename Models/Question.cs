using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class Question
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
    [Required, StringLength(40)] public string QuestionType { get; set; } = "Multiple Choice";
    [Required, StringLength(20)] public string Difficulty { get; set; } = "Medium";
    [Required, StringLength(4000)] public string QuestionText { get; set; } = string.Empty;
    [Required] public string OptionsJson { get; set; } = "[]";
    public int CorrectOption { get; set; }
    [Required, StringLength(4000)] public string ExpectedAnswer { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Explanation { get; set; } = string.Empty;
    public decimal MaximumMarks { get; set; } = 1;
    [StringLength(80)] public string AiProvider { get; set; } = "Gemini";
    [StringLength(120)] public string AiModel { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Status { get; set; } = "AI Generated";
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
