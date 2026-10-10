using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduMation.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedBooksAndLongerBookPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RelativePath",
                table: "SourceBooks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(180)",
                oldMaxLength: 180);

            migrationBuilder.AddColumn<bool>(
                name: "IsManagedFile",
                table: "SourceBooks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Profiles",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EmailVerificationAttempts",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmailVerificationExpiresAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedEmailVerificationCode",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegisteredAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManagedFile",
                table: "SourceBooks");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "EmailVerificationAttempts",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmailVerificationExpiresAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProtectedEmailVerificationCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegisteredAtUtc",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "RelativePath",
                table: "SourceBooks",
                type: "nvarchar(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);
        }
    }
}
