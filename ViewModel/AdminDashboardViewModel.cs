namespace EduMation.ViewModel;

public class AdminDashboardViewModel
{
    public int TotalStudents { get; set; }
    public int ActiveStudents { get; set; }
    public int PublishedLessons { get; set; }
    public int CompletedLessons { get; set; }
    public int PracticeAttempts { get; set; }
    public decimal AveragePracticeScore { get; set; }
    public int PublishedVideos { get; set; }
    public int AiCallsThisMonth { get; set; }
    public int AiFailuresThisMonth { get; set; }
    public double AverageAiLatencyMs { get; set; }
    public IReadOnlyList<AiUsageMetricViewModel> AiCallsByFeature { get; set; } = Array.Empty<AiUsageMetricViewModel>();
    public IReadOnlyList<AiUsageMetricViewModel> AiCallsByProvider { get; set; } = Array.Empty<AiUsageMetricViewModel>();
}

public class AiUsageMetricViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Calls { get; set; }
    public int Failures { get; set; }
    public double AverageMs { get; set; }
}
