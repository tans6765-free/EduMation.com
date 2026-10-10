using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EduMation.Models;

namespace EduMation.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Video> Videos { get; set; }
        public DbSet<Favorites> Favorites { get; set; }
        public DbSet<WatchHistory> WatchHistories { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<LearningClass> LearningClasses { get; set; }
        public DbSet<AcademicYear> AcademicYears { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonVersion> LessonVersions { get; set; }
        public DbSet<ProtijogMapping> ProtijogMappings { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }
        public DbSet<SourceBook> SourceBooks { get; set; }
        public DbSet<QuestionAttempt> QuestionAttempts { get; set; }
        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }
        public DbSet<SavedLesson> SavedLessons { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<CalendarReminder> CalendarReminders { get; set; }
        public DbSet<StudentNotification> StudentNotifications { get; set; }
        public DbSet<StudentAchievement> StudentAchievements { get; set; }
        public DbSet<AiUsageLog> AiUsageLogs { get; set; }
        public DbSet<GoogleCalendarConnection> GoogleCalendarConnections { get; set; }
        public DbSet<SitePage> SitePages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Profile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.HasOne(e => e.User)
                      .WithOne()
                      .HasForeignKey<Profile>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalWatched).HasDefaultValue(0);
            });
            modelBuilder.Entity<QuestionAttempt>().Property(attempt => attempt.Score).HasPrecision(5, 2);
            modelBuilder.Entity<QuestionAttempt>().Property(attempt => attempt.MaximumScore).HasPrecision(5, 2);
            modelBuilder.Entity<Question>().Property(question => question.MaximumMarks).HasPrecision(5, 2);
            modelBuilder.Entity<AdminAuditLog>().HasIndex(item => item.CreatedAtUtc);
            modelBuilder.Entity<SavedLesson>().HasIndex(item => new { item.UserId, item.LessonId }).IsUnique();
            modelBuilder.Entity<SavedLesson>().HasOne(item => item.Lesson).WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Question>().HasIndex(item => new { item.LessonId, item.Status });
            modelBuilder.Entity<Question>().HasOne(item => item.Lesson).WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CalendarReminder>().HasIndex(item => new { item.UserId, item.StartsAt });
            modelBuilder.Entity<StudentNotification>().HasIndex(item => new { item.UserId, item.ReadAtUtc, item.CreatedAtUtc });
            modelBuilder.Entity<StudentAchievement>().HasIndex(item => new { item.UserId, item.Code }).IsUnique();
            modelBuilder.Entity<AiUsageLog>().HasIndex(item => item.CreatedAtUtc);
            modelBuilder.Entity<GoogleCalendarConnection>().HasIndex(item => item.UserId).IsUnique();
            modelBuilder.Entity<SitePage>().HasIndex(item => item.Slug).IsUnique();
            modelBuilder.Entity<SitePage>().HasData(
                new SitePage { Id = 1, Slug = "about", Title = "About EduMation", MetaDescription = "Learn about EduMation and our mission to support learners in Bangladesh.", Body = "EduMation helps learners in Bangladesh follow a clear path through curriculum lessons, practice, and progress.\n\nOur mission is to make structured learning accessible and help every learner build understanding at their own pace.", IsPublished = true, UpdatedAtUtc = new DateTime(2026, 10, 10) },
                new SitePage { Id = 2, Slug = "how-it-works", Title = "How EduMation Works", MetaDescription = "Choose a class, learn a lesson, practice, and track your progress with EduMation.", Body = "1. Choose a class and subject to find lessons organized by chapter and topic.\n\n2. Learn one lesson at a time with reading and video materials where available.\n\n3. Practice with reviewed questions and revisit lessons to strengthen what you learned.\n\n4. Save lessons, continue where you left off, and follow your learning progress.", IsPublished = true, UpdatedAtUtc = new DateTime(2026, 10, 10) },
                new SitePage { Id = 3, Slug = "contact", Title = "Contact EduMation", MetaDescription = "Get help with your EduMation account, lessons, or learning progress.", Body = "For help with your account, lessons, or progress, contact the EduMation support team. Set Support:Email in site configuration to show a support email address here.", IsPublished = true, UpdatedAtUtc = new DateTime(2026, 10, 10) },
                new SitePage { Id = 4, Slug = "privacy", Title = "Privacy Policy", MetaDescription = "How EduMation handles account information and learning data.", Body = "EduMation uses account details to provide learning, saved lessons, progress tracking, and reminders. Keep your profile information current and contact support if you need help with your data.", IsPublished = true, UpdatedAtUtc = new DateTime(2026, 10, 10) });

            modelBuilder.Entity<LearningClass>()
                .HasOne(item => item.AcademicYear).WithMany(item => item.Classes)
                .HasForeignKey(item => item.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<LessonVersion>()
                .HasIndex(item => new { item.LessonId, item.Version }).IsUnique();
            modelBuilder.Entity<LessonVersion>()
                .HasOne(item => item.Lesson).WithMany(item => item.Versions)
                .HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subject>()
                .HasOne(subject => subject.LearningClass)
                .WithMany(learningClass => learningClass.Subjects)
                .HasForeignKey(subject => subject.LearningClassId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Chapter>()
                .HasOne(chapter => chapter.Subject)
                .WithMany(subject => subject.Chapters)
                .HasForeignKey(chapter => chapter.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Topic>()
                .HasOne(topic => topic.Chapter)
                .WithMany(chapter => chapter.Topics)
                .HasForeignKey(topic => topic.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Lesson>()
                .HasOne(lesson => lesson.Topic)
                .WithMany(topic => topic.Lessons)
                .HasForeignKey(lesson => lesson.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Lesson>()
                .HasOne(lesson => lesson.Video)
                .WithMany()
                .HasForeignKey(lesson => lesson.VideoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Lesson>()
                .HasOne(lesson => lesson.ProtijogMapping)
                .WithOne(mapping => mapping.Lesson)
                .HasForeignKey<ProtijogMapping>(mapping => mapping.LessonId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LessonProgress>()
                .HasIndex(progress => new { progress.UserId, progress.LessonId })
                .IsUnique();

            modelBuilder.Entity<LessonProgress>()
                .HasOne(progress => progress.Lesson)
                .WithMany(lesson => lesson.Progress)
                .HasForeignKey(progress => progress.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
