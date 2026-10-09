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
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<ProtijogMapping> ProtijogMappings { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }

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