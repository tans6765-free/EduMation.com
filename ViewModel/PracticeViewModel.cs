using EduMation.Models;

namespace EduMation.ViewModel;

public class PracticeQuestionViewModel
{
    public string Prompt { get; set; } = string.Empty;
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectOption { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class PracticeViewModel
{
    public Lesson Lesson { get; set; } = null!;
    public IReadOnlyList<PracticeQuestionViewModel> Questions { get; set; } = Array.Empty<PracticeQuestionViewModel>();
}
