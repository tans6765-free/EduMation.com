using EduMation.Models;

namespace EduMation.ViewModel;

public class HomeLandingViewModel
{
    public IReadOnlyList<LearningClass> Classes { get; set; } = Array.Empty<LearningClass>();
    public IReadOnlyList<Lesson> FeaturedLessons { get; set; } = Array.Empty<Lesson>();
}
