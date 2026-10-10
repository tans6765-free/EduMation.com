using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduMation.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedLessonsQuestionReviewAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older installations may already have SourceBooks without this field.
            // Add it in that case; otherwise narrow the existing field to the model size.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.SourceBooks', N'ExtractionStatus') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SourceBooks]
                    ADD [ExtractionStatus] nvarchar(40) NOT NULL
                        CONSTRAINT [DF_SourceBooks_ExtractionStatus_Migration]
                        DEFAULT N'NOT_EXTRACTED';
                END
                ELSE
                BEGIN
                    ALTER TABLE [dbo].[SourceBooks]
                    ALTER COLUMN [ExtractionStatus] nvarchar(40) NOT NULL;
                END;
                """);

            migrationBuilder.AddColumn<int>(
                name: "QuestionId",
                table: "QuestionAttempts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Difficulty = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrectOption = table.Column<int>(type: "int", nullable: false),
                    ExpectedAnswer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MaximumMarks = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AiProvider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AiModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedLessons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LessonId = table.Column<int>(type: "int", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedLessons_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_LessonId_Status",
                table: "Questions",
                columns: new[] { "LessonId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedLessons_LessonId",
                table: "SavedLessons",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedLessons_UserId_LessonId",
                table: "SavedLessons",
                columns: new[] { "UserId", "LessonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "SavedLessons");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "QuestionAttempts");

            // Keep ExtractionStatus: Up may have introduced it for a legacy schema.
        }
    }
}
