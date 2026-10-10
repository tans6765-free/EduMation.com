using EduMation.Models;

namespace EduMation.ViewModel;

public class SearchViewModel
{
    public string Query { get; set; } = string.Empty;
    public IReadOnlyList<Lesson> Lessons { get; set; } = Array.Empty<Lesson>();
    public IReadOnlyList<Video> Videos { get; set; } = Array.Empty<Video>();
}
