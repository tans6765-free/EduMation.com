namespace EduMation.ViewModel;

public class QuestionReviewViewModel
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "Multiple Choice";
    public decimal MaximumMarks { get; set; } = 1;
    public string ExpectedAnswer { get; set; } = string.Empty;
    public string OptionsText { get; set; } = string.Empty;
    public int CorrectOption { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public string Decision { get; set; } = "approve";
}
