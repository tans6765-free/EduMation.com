using EduMation.Models;

namespace EduMation.ViewModel;

public class CurriculumImportViewModel
{
    public IReadOnlyList<SourceBook> RegisteredBooks { get; set; } = Array.Empty<SourceBook>();
    public IReadOnlyList<string> DiscoveredFiles { get; set; } = Array.Empty<string>();
}
