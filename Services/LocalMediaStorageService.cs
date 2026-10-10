namespace EduMation.Services;

public sealed class LocalMediaStorageService : IMediaStorageService
{
    private readonly IWebHostEnvironment _environment;
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public LocalMediaStorageService(IWebHostEnvironment environment) => _environment = environment;

    public async Task<string> SaveAsync(IFormFile file, MediaAssetKind kind, CancellationToken cancellationToken = default)
    {
        var maxSize = kind == MediaAssetKind.Video ? 100L * 1024 * 1024 : 10L * 1024 * 1024;
        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        var allowed = kind == MediaAssetKind.Video ? VideoExtensions : ImageExtensions;
        if (file.Length <= 0 || file.Length > maxSize || !allowed.Contains(extension))
            throw new InvalidDataException("The file type or size is not allowed.");

        var mediaTypeAllowed = kind == MediaAssetKind.Video
            ? file.ContentType is "video/mp4" or "video/webm" or "video/quicktime"
            : file.ContentType is "image/jpeg" or "image/png" or "image/webp" or "image/gif";
        if (!mediaTypeAllowed) throw new InvalidDataException("The uploaded media type is not supported.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!HasValidSignature(header, read, extension, kind))
            throw new InvalidDataException("The uploaded file signature does not match its extension.");
        input.Position = 0;

        var category = kind == MediaAssetKind.Video ? "videos" : "thumbnails";
        var folder = Path.Combine(_environment.WebRootPath, "Uploads", category);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(folder, fileName);
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await input.CopyToAsync(output, cancellationToken);
        return $"/Uploads/{category}/{fileName}";
    }

    private static bool HasValidSignature(byte[] data, int length, string extension, MediaAssetKind kind)
    {
        if (kind == MediaAssetKind.Image)
        {
            return extension.ToLowerInvariant() switch
            {
                ".png" => length >= 8 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".jpg" or ".jpeg" => length >= 3 && data[0] == 255 && data[1] == 216 && data[2] == 255,
                ".gif" => length >= 6 && (System.Text.Encoding.ASCII.GetString(data, 0, 6) is "GIF87a" or "GIF89a"),
                ".webp" => length >= 12 && System.Text.Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(data, 8, 4) == "WEBP",
                _ => false
            };
        }

        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".mov" => length >= 8 && System.Text.Encoding.ASCII.GetString(data, 4, 4) == "ftyp",
            ".webm" => length >= 4 && data.AsSpan(0, 4).SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            _ => false
        };
    }
}
