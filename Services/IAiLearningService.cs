using EduMation.Models;
using EduMation.ViewModel;

namespace EduMation.Services;

public interface IAiLearningService
{
    Task<IReadOnlyList<PracticeQuestionViewModel>> GenerateQuestionsAsync(Lesson lesson, CancellationToken cancellationToken = default);
    Task<string> AskTutorAsync(string question, Lesson? lesson = null, string? sourceContext = null, CancellationToken cancellationToken = default);
}
