using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EduMation.Models;
using EduMation.ViewModel;
using Microsoft.Extensions.Options;

namespace EduMation.Services;

public class OpenAiLearningService : IAiLearningService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiLearningService> _logger;

    public OpenAiLearningService(HttpClient httpClient, IOptions<OpenAiOptions> options, ILogger<OpenAiLearningService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }

    public async Task<IReadOnlyList<PracticeQuestionViewModel>> GenerateQuestionsAsync(Lesson lesson, CancellationToken cancellationToken = default)
    {
        var fallback = CreateFallbackQuestions(lesson);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return fallback;
        }

        var context = BuildLessonContext(lesson);
        var prompt = "Create an age-appropriate practice set for this EduMation lesson.\n" +
            "Generate exactly 5 questions: 2 MCQ, 1 true/false, 1 conceptual short-answer represented as two choices, and 1 application question represented as two choices.\n" +
            "Stay strictly inside the supplied lesson context. Do not invent textbook facts.\n" +
            "Return only JSON in this shape: {\"questions\":[{\"prompt\":\"...\",\"options\":[\"...\"],\"correctOption\":0,\"explanation\":\"...\"}]}.\n" +
            "Every correctOption must be a zero-based option index.\n\n" +
            "Lesson context:\n" + context;

        var generated = await RequestJsonAsync(prompt, cancellationToken);
        if (generated == null || !generated.RootElement.TryGetProperty("questions", out var questions))
        {
            return fallback;
        }

        try
        {
            var result = JsonSerializer.Deserialize<List<PracticeQuestionViewModel>>(questions.GetRawText(), JsonOptions);
            return result is { Count: > 0 } ? result : fallback;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "OpenAI returned an invalid practice question payload for lesson {LessonId}.", lesson.Id);
            return fallback;
        }
    }

    public async Task<string> AskTutorAsync(string question, Lesson? lesson = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return "AI Tutor is ready for your NCTB lesson context. Add the server-side OpenAI API key to enable live answers, then ask me about the idea you are studying.";
        }

        var context = lesson == null ? "No lesson selected." : BuildLessonContext(lesson);
        var prompt = $"""
            You are EduMation AI Tutor. Teach rather than simply giving away homework answers.
            Use simple, age-appropriate language. If the question is outside the lesson context, say so and ask for the relevant lesson.
            Lesson context:
            {context}

            Student question:
            {question}
            """;
        var response = await RequestTextAsync(prompt, cancellationToken);
        return response ?? "I could not reach the AI Tutor right now. Please try again in a moment.";
    }

    private async Task<JsonDocument?> RequestJsonAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(new
        {
            model = _options.Model,
            input = prompt,
            text = new { format = new { type = "json_object" } }
        }, cancellationToken);
        if (response == null) return null;

        var text = ExtractText(response.RootElement);
        return string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text);
    }

    private async Task<string?> RequestTextAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await SendRequestAsync(new { model = _options.Model, input = prompt }, cancellationToken);
        return response == null ? null : ExtractText(response.RootElement);
    }

    private async Task<JsonDocument?> SendRequestAsync(object request, CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "responses")
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI request failed with status {StatusCode}.", response.StatusCode);
                return null;
            }
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "OpenAI request failed; using the local EduMation fallback.");
            return null;
        }
    }

    private static string? ExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("text") && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
                var nested = ExtractText(property.Value);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = ExtractText(item);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return null;
    }

    private static string BuildLessonContext(Lesson lesson)
    {
        var topic = lesson.Topic;
        var chapter = topic?.Chapter;
        var subject = chapter?.Subject;
        var learningClass = subject?.LearningClass;
        return $"Class: {learningClass?.Name ?? "Not imported yet"}\nSubject: {subject?.Name ?? "Not imported yet"}\nChapter: {chapter?.Title ?? "Not imported yet"}\nTopic: {topic?.Title ?? "Not imported yet"}\nLesson: {lesson.Title}\nDescription: {lesson.Description}\nObjectives: {lesson.LearningObjectives}";
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
