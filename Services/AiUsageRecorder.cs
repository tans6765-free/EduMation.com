using EduMation.Data;
using EduMation.Models;

namespace EduMation.Services;

public sealed class AiUsageRecorder(ApplicationDbContext db, IConfiguration configuration) : IAiUsageRecorder
{
    public async Task RecordAsync(string feature, long durationMs, bool success, CancellationToken cancellationToken = default)
    {
        var provider = configuration["Ai:Provider"] ?? "Gemini";
        db.AiUsageLogs.Add(new AiUsageLog { Feature = feature, Provider = provider, Model = configuration[$"{provider}:Model"] ?? "configured", Outcome = success ? "Success" : "Failure", DurationMs = Math.Max(durationMs, 0) });
        await db.SaveChangesAsync(cancellationToken);
    }
}
