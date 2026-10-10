using EduMation.Models;

namespace EduMation.ViewModel;

public class CurriculumManagementViewModel
{
    public IReadOnlyList<AcademicYear> AcademicYears { get; set; } = Array.Empty<AcademicYear>();
    public IReadOnlyList<LearningClass> Classes { get; set; } = Array.Empty<LearningClass>();
    public IReadOnlyList<Subject> Subjects { get; set; } = Array.Empty<Subject>();
    public IReadOnlyList<Chapter> Chapters { get; set; } = Array.Empty<Chapter>();
    public IReadOnlyList<Topic> Topics { get; set; } = Array.Empty<Topic>();
    public IReadOnlyList<Lesson> Lessons { get; set; } = Array.Empty<Lesson>();
}

public class LessonEditViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string LearningObjectives { get; set; } = string.Empty;
    public string KeyConcepts { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int DisplayOrder { get; set; }
}
