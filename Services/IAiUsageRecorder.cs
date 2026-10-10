namespace EduMation.Services;

public interface IAiUsageRecorder
{
    Task RecordAsync(string feature, long durationMs, bool success, CancellationToken cancellationToken = default);
}
