using EduMation.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Data;

public sealed class DevelopmentDataSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await context.AcademicYears.AnyAsync(item => item.Name == "2026 Development Sample", cancellationToken)) return;
        var year = new AcademicYear { Name = "2026 Development Sample", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2026, 12, 31), IsCurrent = false };
        var learningClass = new LearningClass { Name = "Class 5", Slug = "class-5", DisplayOrder = 5, IsPublished = true, AcademicYear = year };
        var subject = new Subject { Name = "Mathematics", Slug = "mathematics", DisplayOrder = 1, IsPublished = true, LearningClass = learningClass };
        var chapter = new Chapter { Title = "Fractions", DisplayOrder = 1, IsPublished = true, Subject = subject };
        var topic = new Topic { Title = "Equivalent Fractions", Slug = "equivalent-fractions", DisplayOrder = 1, IsPublished = true, Chapter = chapter };
        var lesson = new Lesson
        {
            Title = "Understanding Equivalent Fractions", Slug = "understanding-equivalent-fractions",
            Description = "Explore how different fractions can represent the same amount.",
            Summary = "Equivalent fractions have different numerators and denominators but represent the same value.",
            LearningObjectives = "Recognize equivalent fractions; Explain what numerator and denominator mean",
            KeyConcepts = "Numerator; Denominator; Equivalent value", DisplayOrder = 1, DurationMinutes = 8,
            IsPublished = true, Topic = topic
        };
        context.AcademicYears.Add(year);
        context.Lessons.Add(lesson);
        await context.SaveChangesAsync(cancellationToken);
    }
}
