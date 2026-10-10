using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EduMation.Migrations
{
    /// <inheritdoc />
    public partial class AddEditableSitePages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SitePages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MetaDescription = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 30000, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitePages", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SitePages",
                columns: new[] { "Id", "Body", "IsPublished", "MetaDescription", "Slug", "Title", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "EduMation helps learners in Bangladesh follow a clear path through curriculum lessons, practice, and progress.\n\nOur mission is to make structured learning accessible and help every learner build understanding at their own pace.", true, "Learn about EduMation and our mission to support learners in Bangladesh.", "about", "About EduMation", new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, "1. Choose a class and subject to find lessons organized by chapter and topic.\n\n2. Learn one lesson at a time with reading and video materials where available.\n\n3. Practice with reviewed questions and revisit lessons to strengthen what you learned.\n\n4. Save lessons, continue where you left off, and follow your learning progress.", true, "Choose a class, learn a lesson, practice, and track your progress with EduMation.", "how-it-works", "How EduMation Works", new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, "For help with your account, lessons, or progress, contact the EduMation support team. Set Support:Email in site configuration to show a support email address here.", true, "Get help with your EduMation account, lessons, or learning progress.", "contact", "Contact EduMation", new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 4, "EduMation uses account details to provide learning, saved lessons, progress tracking, and reminders. Keep your profile information current and contact support if you need help with your data.", true, "How EduMation handles account information and learning data.", "privacy", "Privacy Policy", new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SitePages_Slug",
                table: "SitePages",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SitePages");
        }
    }
}
