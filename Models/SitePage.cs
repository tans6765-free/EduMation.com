using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class SitePage
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Slug { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(240)] public string MetaDescription { get; set; } = string.Empty;
    [Required, StringLength(30000)] public string Body { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
