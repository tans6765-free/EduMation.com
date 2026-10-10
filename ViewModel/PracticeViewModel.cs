using EduMation.Models;

namespace EduMation.ViewModel;

public class PracticeQuestionViewModel
{
    public static readonly string[] SupportedQuestionTypes = { "Multiple Choice", "True/False", "Fill in the Blank", "Short Answer", "Conceptual Understanding", "Application/Problem Solving", "Calculation", "Explanation", "Broad Answer" };
    public static bool IsChoiceType(string type) => type is "Multiple Choice" or "True/False";
    public static decimal MarksFor(string type) => type switch
    {
        "Multiple Choice" or "True/False" or "Fill in the Blank" => 1,
        "Short Answer" or "Conceptual Understanding" or "Application/Problem Solving" or "Calculation" => 3,
        "Explanation" or "Broad Answer" => 5,
        _ => 0
    };
    public static bool IsValid(PracticeQuestionViewModel item) => SupportedQuestionTypes.Contains(item.QuestionType) &&
        !string.IsNullOrWhiteSpace(item.Prompt) && !string.IsNullOrWhiteSpace(item.ExpectedAnswer) &&
        !string.IsNullOrWhiteSpace(item.Explanation) && item.MaximumMarks == MarksFor(item.QuestionType) &&
        (IsChoiceType(item.QuestionType)
            ? item.Options is { Length: >= 2 and <= 6 } && item.CorrectOption >= 0 && item.CorrectOption < item.Options.Length && (item.QuestionType != "True/False" || item.Options.Length == 2)
            : item.Options is { Length: 0 } && item.CorrectOption == -1);

    public int QuestionId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string[] Options { get; set; } = Array.Empty<string>();
    public int CorrectOption { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "Multiple Choice";
    public decimal MaximumMarks { get; set; } = 1;
    public string ExpectedAnswer { get; set; } = string.Empty;
}

public class PracticeViewModel
{
    public Lesson Lesson { get; set; } = null!;
    public string PracticeSessionId { get; set; } = string.Empty;
    public IReadOnlyList<PracticeQuestionViewModel> Questions { get; set; } = Array.Empty<PracticeQuestionViewModel>();
}
