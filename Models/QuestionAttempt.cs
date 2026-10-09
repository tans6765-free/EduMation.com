using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class QuestionAttempt
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public int? LessonId { get; set; }
    [Required, StringLength(4000)] public string QuestionText { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string StudentAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public decimal Score { get; set; }
    [StringLength(1200)] public string Feedback { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
