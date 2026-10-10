using EduMation.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMation.Data;

/// <summary>Seeds class access and the subject families described by the supplied 2026 planning files.</summary>
public sealed class NctbCatalogSeeder(ApplicationDbContext db)
{
    private static readonly string[] EarlyPrimary = ["Bangla", "English", "Mathematics"];
    private static readonly string[] Primary = ["Bangla", "English", "Mathematics", "Science", "Bangladesh and Global Studies", "Islamic Studies and Moral Education", "Hindu Religion Studies", "Buddhist Religion Studies", "Christian Religion Studies"];
    private static readonly string[] Middle = ["Bangla Literature", "Bangla Grammar and Composition", "English for Today", "English Grammar and Composition", "Mathematics", "Science", "Bangladesh and Global Studies", "Information and Communication Technology", "Physical Education and Health", "Work and Life-Oriented Education", "Agriculture Studies", "Home Science", "Arts and Crafts", "Islamic Studies", "Hindu Religion Studies", "Buddhist Religion Studies", "Christian Religion Studies"];
    private static readonly string[] Secondary = ["Bangla Literature", "Bangla Grammar and Composition", "English for Today", "English Grammar and Composition", "Mathematics", "Information and Communication Technology", "Bangladesh and Global Studies", "Physical Education and Health", "Career Education", "Islamic Studies", "Hindu Religion Studies", "Christian Religion Studies", "Buddhist Religion Studies", "Physics", "Chemistry", "Biology", "Higher Mathematics", "History of Bangladesh and World Civilization", "Geography and Environment", "Civics and Citizenship", "Economics", "Accounting", "Finance and Banking", "Business Entrepreneurship", "Agriculture Studies", "Home Science", "Arts and Crafts"];
    private static readonly string[] HigherSecondary = ["Sahityapath", "Sahapath", "English for Today", "Information and Communication Technology", "Physics — First Paper", "Physics — Second Paper", "Chemistry — First Paper", "Chemistry — Second Paper", "Biology — First Paper", "Biology — Second Paper", "Higher Mathematics — First Paper", "Higher Mathematics — Second Paper", "History", "Islamic History and Culture", "Civics and Good Governance", "Economics", "Geography", "Sociology", "Social Work", "Logic", "Psychology", "Accounting", "Business Organization and Management", "Finance, Banking and Insurance", "Production Management and Marketing"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var year = await db.AcademicYears.FirstOrDefaultAsync(item => item.Name == "2026", cancellationToken);
        if (year == null)
        {
            year = new AcademicYear { Name = "2026", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2026, 12, 31), IsCurrent = true };
            db.AcademicYears.Add(year);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!year.IsCurrent)
        {
            year.IsCurrent = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        for (var grade = 1; grade <= 12; grade++)
        {
            var candidates = await db.LearningClasses.Include(item => item.Subjects)
                .Where(item => item.Slug == $"class-{grade}-2026" || item.Slug == $"class-{grade}")
                .OrderByDescending(item => item.Slug == $"class-{grade}-2026").ToListAsync(cancellationToken);
            var learningClass = candidates.FirstOrDefault();
            if (learningClass == null)
            {
                learningClass = new LearningClass { Name = $"Class {grade}", Slug = $"class-{grade}-2026", DisplayOrder = grade, IsPublished = true, AcademicYearId = year.Id };
                db.LearningClasses.Add(learningClass);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                learningClass.Name = $"Class {grade}";
                learningClass.AcademicYearId = year.Id;
                learningClass.IsPublished = true;
            }
            await db.Profiles.Where(profile => profile.LearningClassId == learningClass.Id && profile.AcademicYearId != year.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.AcademicYearId, (int?)year.Id), cancellationToken);
            foreach (var duplicate in candidates.Where(item => item.Id != learningClass.Id && item.IsPublished))
            {
                duplicate.IsPublished = false;
                await db.Profiles.Where(profile => profile.LearningClassId == duplicate.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.LearningClassId, learningClass.Id)
                        .SetProperty(profile => profile.AcademicYearId, learningClass.AcademicYearId), cancellationToken);
            }

            var subjects = grade <= 2 ? EarlyPrimary : grade <= 5 ? Primary : grade <= 8 ? Middle : grade <= 10 ? Secondary : HigherSecondary;
            var existingSlugs = learningClass.Subjects.Select(item => item.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var order = 0; order < subjects.Length; order++)
            {
                var name = subjects[order];
                var slug = Slug(name);
                if (slug.Length == 0) slug = $"subject-{grade}-{order + 1}";
                if (existingSlugs.Add(slug)) db.Subjects.Add(new Subject { LearningClassId = learningClass.Id, Name = name, Slug = slug, DisplayOrder = order + 1, IsPublished = true });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Slug(string name) => string.Join('-', name.ToLowerInvariant().Split(new[] { ' ', ',', '—', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(part => new string(part.Where(char.IsAsciiLetterOrDigit).ToArray())).Where(part => part.Length > 0));
}
