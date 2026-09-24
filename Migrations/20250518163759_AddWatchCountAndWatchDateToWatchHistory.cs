using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduMation.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchCountAndWatchDateToWatchHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistories_AspNetUsers_ApplicationUserId",
                table: "WatchHistories");

            migrationBuilder.DropIndex(
                name: "IX_WatchHistories_ApplicationUserId",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "WatchHistories");

            migrationBuilder.RenameColumn(
                name: "LastWatched",
                table: "WatchHistories",
                newName: "WatchDate");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "WatchHistories",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "WatchCount",
                table: "WatchHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastResetMonth",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistories_UserId",
                table: "WatchHistories",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistories_AspNetUsers_UserId",
                table: "WatchHistories",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WatchHistories_AspNetUsers_UserId",
                table: "WatchHistories");

            migrationBuilder.DropIndex(
                name: "IX_WatchHistories_UserId",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "WatchCount",
                table: "WatchHistories");

            migrationBuilder.DropColumn(
                name: "LastResetMonth",
                table: "Subscriptions");

            migrationBuilder.RenameColumn(
                name: "WatchDate",
                table: "WatchHistories",
                newName: "LastWatched");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "WatchHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "WatchHistories",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchHistories_ApplicationUserId",
                table: "WatchHistories",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchHistories_AspNetUsers_ApplicationUserId",
                table: "WatchHistories",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
