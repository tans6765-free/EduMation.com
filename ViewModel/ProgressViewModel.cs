using EduMation.Models;

namespace EduMation.ViewModel;

public class ProgressViewModel
{
    public int CompletedLessons { get; set; }
    public int TotalLessons { get; set; }
    public int OverallPercent => TotalLessons == 0 ? 0 : (int)Math.Round(CompletedLessons * 100d / TotalLessons);
    public IReadOnlyList<LessonProgress> RecentProgress { get; set; } = Array.Empty<LessonProgress>();
}
