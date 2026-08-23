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
            migrationBuilder.Sql("""
                SET @drop_sql = IF(
                    EXISTS(
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = DATABASE()
                          AND table_name = 'matches'
                          AND column_name = 'participant_count'
                    ),
                    'ALTER TABLE `matches` DROP COLUMN `participant_count`',
                    'DO 0'
                );
                PREPARE drop_statement FROM @drop_sql;
                EXECUTE drop_statement;
                DEALLOCATE PREPARE drop_statement;
                """);
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
