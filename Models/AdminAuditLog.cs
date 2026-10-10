using System.ComponentModel.DataAnnotations;

namespace EduMation.Models;

public class AdminAuditLog
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string AdminUserId { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Action { get; set; } = string.Empty;
    [Required, StringLength(80)] public string EntityName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string EntityId { get; set; } = string.Empty;
    [StringLength(500)] public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
