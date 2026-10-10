using System.ComponentModel.DataAnnotations;
using EduMation.Models;

namespace EduMation.ViewModel;

public sealed class AdminMessageViewModel
{
    public int? LearningClassId { get; set; }
    public string? StudentId { get; set; }
    [Required, StringLength(120)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(5000)] public string Message { get; set; } = string.Empty;
    public IReadOnlyList<LearningClass> Classes { get; set; } = Array.Empty<LearningClass>();
    public IReadOnlyList<StudentRecipientOption> Students { get; set; } = Array.Empty<StudentRecipientOption>();
}

public sealed record StudentRecipientOption(string Id, string DisplayName, string? Email, int? LearningClassId);
