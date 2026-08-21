using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStrayMatchParticipantCountColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Untracked column from outside the EF model; NOT NULL with no default blocked every match insert.
            migrationBuilder.DropColumn(
                name: "participant_count",
                table: "matches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "participant_count",
                table: "matches",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
