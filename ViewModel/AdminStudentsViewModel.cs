using EduMation.Models;

namespace EduMation.ViewModel;

public sealed class AdminStudentsViewModel
{
    public int? LearningClassId { get; set; }
    public string? BadgeCode { get; set; }
    public DateTime? RegisteredFrom { get; set; }
    public DateTime? RegisteredTo { get; set; }
    public int? MinimumProgress { get; set; }
    public int? MaximumProgress { get; set; }
    public IReadOnlyList<LearningClass> Classes { get; set; } = Array.Empty<LearningClass>();
    public IReadOnlyList<BadgeFilterOption> Badges { get; set; } = Array.Empty<BadgeFilterOption>();
    public IReadOnlyList<AdminStudentRow> Students { get; set; } = Array.Empty<AdminStudentRow>();
}

public sealed record BadgeFilterOption(string Code, string Title);

public sealed record AdminStudentRow(string UserId, string FullName, string Email, string District, string ContactNumber,
    string ClassName, int? LearningClassId, DateTimeOffset RegisteredAtUtc, int ProgressPercent, IReadOnlyList<string> Badges);
