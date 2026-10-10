namespace EduMation.Services;

public interface IStudentEngagementService
{
    Task AwardAchievementAsync(string userId, string code, string title, string description, string icon, CancellationToken cancellationToken = default);
    Task NotifyPracticeCompleteAsync(string userId, decimal score, decimal maximumScore, decimal lifetimeMarks, CancellationToken cancellationToken = default);
}
