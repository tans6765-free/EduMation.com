using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduMation.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubscriptionModelToTotalWatched : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideosWatchedThisMonth",
                table: "Subscriptions");

            migrationBuilder.RenameColumn(
                name: "TotalWatchedVideosSinceSubscription",
                table: "Subscriptions",
                newName: "TotalWatched");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalWatched",
                table: "Subscriptions",
                newName: "TotalWatchedVideosSinceSubscription");

            migrationBuilder.AddColumn<int>(
                name: "VideosWatchedThisMonth",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
