using UglyToad.PdfPig;

namespace EduMation.Services;

public class NctbPdfExtractionService
{
    private readonly ILogger<NctbPdfExtractionService> _logger;

    public NctbPdfExtractionService(ILogger<NctbPdfExtractionService> logger)
    {
        _logger = logger;
    }

    public PdfExtractionResult Extract(string path)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            var pages = document.GetPages().ToList();
            var text = string.Join(Environment.NewLine, pages.Select(page => page.Text));
            return new PdfExtractionResult(pages.Count, text, "EXTRACTED");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not extract text from NCTB PDF {Path}.", path);
            return new PdfExtractionResult(0, string.Empty, "EXTRACTION_FAILED");
        }
    }
}

public record PdfExtractionResult(int PageCount, string Text, string Status);