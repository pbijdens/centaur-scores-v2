using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedResultsSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "published_slug",
                table: "matches",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "published_slug",
                table: "competitions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_matches_published_slug",
                table: "matches",
                column: "published_slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competitions_published_slug",
                table: "competitions",
                column: "published_slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_matches_published_slug",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_competitions_published_slug",
                table: "competitions");

            migrationBuilder.DropColumn(
                name: "published_slug",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "published_slug",
                table: "competitions");
        }
    }
}
