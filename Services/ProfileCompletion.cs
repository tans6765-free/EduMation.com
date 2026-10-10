using EduMation.Models;

namespace EduMation.Services;

public static class ProfileCompletion
{
    public const string AchievementCode = "profile-complete";
    public const string AchievementTitle = "Profile complete";

    public static int Percentage(Profile profile, bool emailVerified)
    {
        var complete = 0;
        if (!string.IsNullOrWhiteSpace(profile.FullName)) complete++;
        if (emailVerified && !string.IsNullOrWhiteSpace(profile.Email)) complete++;
        if (profile.LearningClassId.HasValue) complete++;
        if (!string.IsNullOrWhiteSpace(profile.District)) complete++;
        return complete * 25;
    }

    public static bool IsComplete(Profile profile, bool emailVerified) => Percentage(profile, emailVerified) == 100;
}
