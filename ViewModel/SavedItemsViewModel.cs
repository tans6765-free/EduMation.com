using EduMation.Models;

namespace EduMation.ViewModel;

public class SavedItemsViewModel
{
    public IReadOnlyList<Video> Videos { get; set; } = Array.Empty<Video>();
    public IReadOnlyList<SavedLesson> Lessons { get; set; } = Array.Empty<SavedLesson>();
}
