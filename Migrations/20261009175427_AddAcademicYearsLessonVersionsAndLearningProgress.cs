using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduMation.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearsLessonVersionsAndLearningProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LastPositionSeconds",
                table: "WatchHistories",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "WatchHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "WatchHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "Profiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LearningClassId",
                table: "Profiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LearningPreferences",
                table: "Profiles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Profiles",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Lessons",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<string>(
                name: "KeyConcepts",
                table: "Lessons",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Lessons",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Lessons",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Lessons",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "LessonProgresses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastPositionSeconds",
                table: "LessonProgresses",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "LessonProgresses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcademicYearId",
                table: "LearningClasses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LessonVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LearningObjectives = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KeyConcepts = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonVersions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // These foundation tables were historically provisioned by startup SQL. Keep the
            // migration safe for both those existing installations and clean databases.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[QuestionAttempts]') IS NULL
                BEGIN
                    CREATE TABLE [QuestionAttempts] (
                        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_QuestionAttempts] PRIMARY KEY,
                        [UserId] nvarchar(max) NOT NULL,
                        [LessonId] int NULL,
                        [QuestionText] nvarchar(4000) NOT NULL,
                        [StudentAnswer] nvarchar(4000) NOT NULL,
                        [IsCorrect] bit NOT NULL,
                        [Score] decimal(18,2) NOT NULL,
                        [Feedback] nvarchar(1200) NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL
                    );
                END;
                IF OBJECT_ID(N'[SourceBooks]') IS NULL
                BEGIN
                    CREATE TABLE [SourceBooks] (
                        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SourceBooks] PRIMARY KEY,
                        [OriginalFileName] nvarchar(260) NOT NULL,
                        [RelativePath] nvarchar(180) NOT NULL,
                        [ClassLevel] nvarchar(80) NOT NULL,
                        [LanguageVersion] nvarchar(80) NOT NULL,
                        [Subject] nvarchar(160) NOT NULL,
                        [AcademicYear] nvarchar(40) NOT NULL,
                        [VerificationStatus] nvarchar(60) NOT NULL,
                        [ImportedAtUtc] datetime2 NOT NULL,
                        [IsActive] bit NOT NULL,
                        [PageCount] int NOT NULL CONSTRAINT [DF_SourceBooks_PageCount] DEFAULT 0,
                        [ExtractedText] nvarchar(max) NOT NULL CONSTRAINT [DF_SourceBooks_ExtractedText] DEFAULT N'',
                        [ExtractionStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_SourceBooks_ExtractionStatus] DEFAULT N'NOT_EXTRACTED'
                    );
                    CREATE UNIQUE INDEX [IX_SourceBooks_RelativePath] ON [SourceBooks] ([RelativePath]);
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_AcademicYearId",
                table: "Profiles",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_LearningClassId",
                table: "Profiles",
                column: "LearningClassId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningClasses_AcademicYearId",
                table: "LearningClasses",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_LessonId_Version",
                table: "LessonVersions",
                columns: new[] { "LessonId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningClasses_AcademicYears_AcademicYearId",
                table: "LearningClasses",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_AcademicYears_AcademicYearId",
                table: "Profiles",
                column: "AcademicYearId",
                principalTable: "AcademicYears",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_LearningClasses_LearningClassId",
                table: "Profiles",
                column: "LearningClassId",
                principalTable: "LearningClasses",
                principalColumn: "Id");

            // Legacy sample curriculum had no academic-year identity. Keep it out of the
            // public catalog until an administrator assigns and republishes it.
            migrationBuilder.Sql("UPDATE [LearningClasses] SET [IsPublished] = 0 WHERE [AcademicYearId] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningClasses_AcademicYears_AcademicYearId",
                table: "LearningClasses");

            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_AcademicYears_AcademicYearId",
                table: "Profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_LearningClasses_LearningClassId",
                table: "Profiles");

            migrationBuilder.DropTable(
                name: "AcademicYears");

            migrationBuilder.DropTable(
                name: "LessonVersions");

            // Retain these long-lived foundation tables on rollback; previous deployments
            // created them outside migrations and they contain imported/learner data.

            migrationBuilder.DropIndex(
                name: "IX_Profiles_AcademicYearId",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_LearningClassId",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_LearningClasses_AcademicYearId",
                table: "LearningClasses");

            migrationBuilder.DropColumn(
                name: "LastPositionSeconds",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "LearningClassId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "LearningPreferences",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "KeyConcepts",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "LastPositionSeconds",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "LessonProgresses");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "LearningClasses");
        }
    }
}
