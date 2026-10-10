using System.ComponentModel.DataAnnotations;

namespace EduMation.ViewModel;

public class SitePageViewModel
{
    public int Id { get; set; }
    [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(240)] public string MetaDescription { get; set; } = string.Empty;
    [Required, StringLength(30000)] public string Body { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
}
