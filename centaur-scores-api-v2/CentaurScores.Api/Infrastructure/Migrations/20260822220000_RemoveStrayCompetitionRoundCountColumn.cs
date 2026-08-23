using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260822220000_RemoveStrayCompetitionRoundCountColumn")]
    public partial class RemoveStrayCompetitionRoundCountColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Untracked column from outside the EF model; NOT NULL with no default blocked every competition insert.
            migrationBuilder.Sql("""
                SET @drop_sql = IF(
                    EXISTS(
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = DATABASE()
                          AND table_name = 'competitions'
                          AND column_name = 'round_count'
                    ),
                    'ALTER TABLE `competitions` DROP COLUMN `round_count`',
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
                name: "round_count",
                table: "competitions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
