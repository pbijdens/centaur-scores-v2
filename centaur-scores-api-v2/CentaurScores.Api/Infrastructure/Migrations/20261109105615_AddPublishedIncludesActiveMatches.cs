using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedIncludesActiveMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows default to true (include active matches), matching the entity default for new rows.
            migrationBuilder.AddColumn<bool>(
                name: "published_includes_active_matches",
                table: "matches",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "published_includes_active_matches",
                table: "competitions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "published_includes_active_matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "published_includes_active_matches",
                table: "competitions");
        }
    }
}
