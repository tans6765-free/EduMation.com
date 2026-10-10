using System.ComponentModel.DataAnnotations;

namespace EduMation.ViewModel;

public sealed class AdminWelcomeEmailViewModel
{
    [Required, StringLength(160)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(30000)]
    public string Body { get; set; } = string.Empty;
}
