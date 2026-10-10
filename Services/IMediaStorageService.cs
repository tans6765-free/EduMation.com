namespace EduMation.Services;

public enum MediaAssetKind { Video, Image }

public interface IMediaStorageService
{
    Task<string> SaveAsync(IFormFile file, MediaAssetKind kind, CancellationToken cancellationToken = default);
}
