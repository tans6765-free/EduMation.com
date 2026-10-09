using EduMation.Models;

namespace EduMation.ViewModel;

public class LearnIndexViewModel
{
    public IReadOnlyList<LearningClass> Classes { get; set; } = Array.Empty<LearningClass>();
    public IReadOnlyList<Lesson> FeaturedLessons { get; set; } = Array.Empty<Lesson>();
}

public class SubjectLearningViewModel
{
    public Subject Subject { get; set; } = null!;
    public IReadOnlyList<Chapter> Chapters { get; set; } = Array.Empty<Chapter>();
}

public class LessonDetailsViewModel
{
    public Lesson Lesson { get; set; } = null!;
    public int ProgressPercent { get; set; }
    public bool IsCompleted { get; set; }
}
