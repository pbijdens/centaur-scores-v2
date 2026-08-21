using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveDeviceSelectionModeToMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "participant_order_json",
                table: "score_devices");

            migrationBuilder.DropColumn(
                name: "selection_mode",
                table: "score_devices");

            migrationBuilder.RenameColumn(
                name: "participant_selection_mode",
                table: "match_templates",
                newName: "device_selection_mode");

            migrationBuilder.AddColumn<string>(
                name: "device_selection_mode",
                table: "matches",
                type: "longtext",
                nullable: false,
                defaultValue: "restricted")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "allow_free_participants",
                table: "match_templates",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "device_selection_mode",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "allow_free_participants",
                table: "match_templates");

            migrationBuilder.RenameColumn(
                name: "device_selection_mode",
                table: "match_templates",
                newName: "participant_selection_mode");

            migrationBuilder.AddColumn<string>(
                name: "participant_order_json",
                table: "score_devices",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "selection_mode",
                table: "score_devices",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
