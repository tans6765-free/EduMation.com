using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class SourceBook
{
    public int Id { get; set; }

    [Required, StringLength(260)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(180)]
    public string RelativePath { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string ClassLevel { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string LanguageVersion { get; set; } = string.Empty;

    [StringLength(160)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(40)]
    public string AcademicYear { get; set; } = "2026";

    [Required, StringLength(60)]
    public string VerificationStatus { get; set; } = "NEEDS_REVIEW";

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
