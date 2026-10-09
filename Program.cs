using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EduMation.Data;
using EduMation.Models;
using EduMation.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Kestrel to allow larger file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100L * 1024L * 1024L; // 100 MB
});

// Configure form options to handle large file uploads
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100L * 1024L * 1024L; // 100 MB
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient<IAiLearningService, GeminiLearningService>();
builder.Services.Configure<NctbContentOptions>(builder.Configuration.GetSection("Nctb"));
builder.Services.AddSingleton<NctbPdfExtractionService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage(); // Enable detailed error pages in development
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    try
    {
        // Ensure database is created and migrations are applied
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[SourceBooks]') IS NULL
            BEGIN
                CREATE TABLE [SourceBooks] (
                    [Id] int NOT NULL IDENTITY,
                    [OriginalFileName] nvarchar(260) NOT NULL,
                    [RelativePath] nvarchar(180) NOT NULL,
                    [ClassLevel] nvarchar(80) NOT NULL,
                    [LanguageVersion] nvarchar(80) NOT NULL,
                    [Subject] nvarchar(160) NOT NULL,
                    [AcademicYear] nvarchar(40) NOT NULL,
                    [VerificationStatus] nvarchar(60) NOT NULL,
                    [ImportedAtUtc] datetime2 NOT NULL,
                    [IsActive] bit NOT NULL,
                    CONSTRAINT [PK_SourceBooks] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_SourceBooks_RelativePath] ON [SourceBooks] ([RelativePath]);
            END
            IF COL_LENGTH(N'[SourceBooks]', N'PageCount') IS NULL ALTER TABLE [SourceBooks] ADD [PageCount] int NOT NULL CONSTRAINT [DF_SourceBooks_PageCount] DEFAULT 0;
            IF COL_LENGTH(N'[SourceBooks]', N'ExtractedText') IS NULL ALTER TABLE [SourceBooks] ADD [ExtractedText] nvarchar(max) NOT NULL CONSTRAINT [DF_SourceBooks_ExtractedText] DEFAULT N'';
            IF COL_LENGTH(N'[SourceBooks]', N'ExtractionStatus') IS NULL ALTER TABLE [SourceBooks] ADD [ExtractionStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_SourceBooks_ExtractionStatus] DEFAULT N'NOT_EXTRACTED';
            IF OBJECT_ID(N'[QuestionAttempts]') IS NULL
            BEGIN
                CREATE TABLE [QuestionAttempts] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] nvarchar(450) NOT NULL,
                    [LessonId] int NULL,
                    [QuestionText] nvarchar(4000) NOT NULL,
                    [StudentAnswer] nvarchar(4000) NOT NULL,
                    [IsCorrect] bit NOT NULL,
                    [Score] decimal(5,2) NOT NULL,
                    [Feedback] nvarchar(1200) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_QuestionAttempts] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_QuestionAttempts_UserId] ON [QuestionAttempts] ([UserId]);
            END
            """);
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations.");
        throw; // Rethrow to surface the error when running
    }

    try
    {
        // Seed roles
        foreach (var role in new[] { "Admin", "Tutor", "Editor" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("{Role} role created.", role);
            }
        }

        // Seed admin user
        var adminEmail = "admin@edumation.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user created and assigned to Admin role.");

                // Seed admin profile if not exists
                var adminProfile = await context.Profiles.FirstOrDefaultAsync(p => p.UserId == adminUser.Id);
                if (adminProfile == null)
                {
                    adminProfile = new Profile
                    {
                        UserId = adminUser.Id,
                        FirstName = "Admin",
                        LastName = "User",
                        Email = adminEmail,
                        Address = "Not specified",
                    };
                    context.Profiles.Add(adminProfile);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Admin profile seeded.");
                }
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Seed sample videos
        if (!context.Videos.Any())
        {
            context.Videos.AddRange(
                new Video
                {
                    Title = "Introduction to Programming",
                    Genre = "Education",
                    Description = "Learn the basics of programming.",
                    VideoUrl = "/Uploads/videos/sample-video.mp4",
                    ThumbnailUrl = "/Uploads/thumbnails/sample-thumbnail.jpg",
                    UploadDate = DateTime.Now
                },
                new Video
                {
                    Title = "Advanced Mathematics",
                    Genre = "Education",
                    Description = "Explore advanced mathematical concepts.",
                    VideoUrl = "/Uploads/videos/math-video.mp4",
                    ThumbnailUrl = "/Uploads/thumbnails/math-thumbnail.jpg",
                    UploadDate = DateTime.Now
                }
            );
            await context.SaveChangesAsync();
            logger.LogInformation("Sample videos seeded.");
        }

        if (!context.LearningClasses.Any())
        {
            var classFive = new LearningClass
            {
                Name = "Class 5",
                Slug = "class-5",
                DisplayOrder = 5
            };
            var mathematics = new Subject
            {
                Name = "Mathematics",
                Slug = "mathematics",
                DisplayOrder = 1,
                LearningClass = classFive
            };
            var fractions = new Chapter
            {
                Title = "Fractions",
                DisplayOrder = 1,
                Subject = mathematics
            };
            var equivalentFractions = new Topic
            {
                Title = "Equivalent Fractions",
                Slug = "equivalent-fractions",
                DisplayOrder = 1,
                Chapter = fractions
            };
            var sampleVideo = await context.Videos.OrderBy(video => video.Id).FirstOrDefaultAsync();
            var lesson = new Lesson
            {
                Title = "Understanding Equivalent Fractions",
                Slug = "understanding-equivalent-fractions",
                Description = "Build a clear mental model for comparing and creating equivalent fractions.",
                LearningObjectives = "Recognize equivalent fractions; Compare fractions with the same value; Prepare for deeper practice",
                DurationMinutes = 12,
                DisplayOrder = 1,
                Topic = equivalentFractions,
                Video = sampleVideo
            };

            context.LearningClasses.Add(classFive);
            context.Lessons.Add(lesson);
            await context.SaveChangesAsync();

            context.ProtijogMappings.Add(new ProtijogMapping
            {
                LessonId = lesson.Id,
                PracticeType = "Topic",
                Title = "Practice this topic on Protijog",
                ExternalPath = "/protijog-coming-soon",
                IsActive = true
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Starter curriculum and Protijog placeholder seeded.");
        }

        var missingClasses = Enumerable.Range(1, 12)
            .Where(number => !context.LearningClasses.Any(learningClass => learningClass.Slug == $"class-{number}"))
            .Select(number => new LearningClass
            {
                Name = $"Class {number}",
                Slug = $"class-{number}",
                DisplayOrder = number,
                IsPublished = true
            })
            .ToList();
        if (missingClasses.Count > 0)
        {
            context.LearningClasses.AddRange(missingClasses);
            await context.SaveChangesAsync();
            logger.LogInformation("Visible curriculum sections ensured for classes 1 through 12.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw; // Rethrow to surface the error when running
    }
}

app.Run();