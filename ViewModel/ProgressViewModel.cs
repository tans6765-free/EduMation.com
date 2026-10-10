using EduMation.Models;

namespace EduMation.ViewModel;

public class ProgressViewModel
{
    public int CompletedLessons { get; set; }
    public int TotalLessons { get; set; }
    public int OverallPercent => TotalLessons == 0 ? 0 : (int)Math.Round(CompletedLessons * 100d / TotalLessons);
    public decimal PracticeMarks { get; set; }
    public int Level => 1 + (int)Math.Floor(Math.Max(0, PracticeMarks) / 50m);
    public IReadOnlyList<LessonProgress> RecentProgress { get; set; } = Array.Empty<LessonProgress>();
    public IReadOnlyList<StudentAchievement> Achievements { get; set; } = Array.Empty<StudentAchievement>();
}
