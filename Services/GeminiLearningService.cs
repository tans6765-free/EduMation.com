using System.Net.Http.Json;
using System.Text.Json;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.Extensions.Options;

namespace EduMation.Services;

public class GeminiLearningService : IAiLearningService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiLearningService> _logger;

    public GeminiLearningService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiLearningService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PracticeQuestionViewModel>> GenerateQuestionsAsync(Lesson lesson, CancellationToken cancellationToken = default)
    {
        var fallback = CreateFallbackQuestions(lesson);
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return fallback;

        var prompt = $"Create exactly five age-appropriate practice questions for this EduMation lesson. Use only this context. Return JSON with a questions array; each item must have prompt, options, correctOption, and explanation. Context: {BuildLessonContext(lesson)}";
        var text = await GenerateTextAsync(prompt, cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        try
        {
            var json = ExtractJson(text);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("questions", out var questions)) return fallback;
            return JsonSerializer.Deserialize<List<PracticeQuestionViewModel>>(questions.GetRawText(), JsonOptions) ?? fallback;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Gemini returned an invalid practice payload for lesson {LessonId}.", lesson.Id);
            return fallback;
        }
    }

    public async Task<string> AskTutorAsync(string question, Lesson? lesson = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return "Gemini Tutor is ready. Add the server-side Gemini API key to enable live, source-grounded answers.";
        }

        var context = lesson == null ? "No lesson context was selected." : BuildLessonContext(lesson);
        var prompt = $"You are EduMation Gemini Tutor. Teach instead of simply giving away homework answers. Use age-appropriate language and answer in the student's language. Do not invent NCTB facts. Lesson context: {context}. Student question: {question}";
        return await GenerateTextAsync(prompt, cancellationToken) ?? "Gemini is unavailable right now. Please try again.";
    }

    private async Task<string?> GenerateTextAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/{_options.Model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
            using var response = await _httpClient.PostAsJsonAsync(endpoint, new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.2 }
            }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini request failed with status {StatusCode}.", response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            _logger.LogWarning(exception, "Gemini request failed; using the local EduMation fallback.");
            return null;
        }
    }

    private static string BuildLessonContext(Lesson lesson) => $"Class: {lesson.Topic?.Chapter?.Subject?.LearningClass?.Name ?? "Not imported"}; Subject: {lesson.Topic?.Chapter?.Subject?.Name ?? "Not imported"}; Chapter: {lesson.Topic?.Chapter?.Title ?? "Not imported"}; Topic: {lesson.Topic?.Title ?? "Not imported"}; Lesson: {lesson.Title}; Description: {lesson.Description}; Objectives: {lesson.LearningObjectives}";

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static IReadOnlyList<PracticeQuestionViewModel> CreateFallbackQuestions(Lesson lesson) => new[]
    {
        new PracticeQuestionViewModel
        {
            Prompt = $"Which idea is the focus of '{lesson.Title}'?",
            Options = new[] { lesson.Topic?.Title ?? lesson.Title, "An unrelated topic", "A random challenge", "None of these" },
            CorrectOption = 0,
            Explanation = "The lesson topic is the starting point for understanding this concept."
        },
        new PracticeQuestionViewModel
        {
            Prompt = "Should you be able to explain the main idea in your own words after this lesson?",
            Options = new[] { "Yes, understanding matters", "No, memorising is enough" },
            CorrectOption = 0,
            Explanation = "Explaining an idea in your own words is a useful sign that the concept is becoming yours."
        }
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
