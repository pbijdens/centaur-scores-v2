using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <summary>
    /// Moves the scorecard signature images out of match_participants into their own table, so queries that
    /// load participants (especially joined with arrow_scores) no longer drag the large data-URL strings
    /// along. Existing signatures are copied over before the old columns are dropped, in both directions.
    /// </summary>
    public partial class MoveSignaturesToSeparateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_participant_signatures",
                columns: table => new
                {
                    match_participant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    tenant_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    archer_signature_data_url = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    marker_signature_data_url = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_participant_signatures", x => x.match_participant_id);
                    table.ForeignKey(
                        name: "fk_match_participant_signatures_match_participants_match_partic",
                        column: x => x.match_participant_id,
                        principalTable: "match_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_match_participant_signatures_tenant_id",
                table: "match_participant_signatures",
                column: "tenant_id");

            migrationBuilder.Sql(
                """
                INSERT INTO match_participant_signatures (match_participant_id, tenant_id, archer_signature_data_url, marker_signature_data_url)
                SELECT id, tenant_id, archer_signature_data_url, marker_signature_data_url
                FROM match_participants
                WHERE archer_signature_data_url IS NOT NULL OR marker_signature_data_url IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "archer_signature_data_url",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "marker_signature_data_url",
                table: "match_participants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "archer_signature_data_url",
                table: "match_participants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "marker_signature_data_url",
                table: "match_participants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                """
                UPDATE match_participants p
                JOIN match_participant_signatures s ON s.match_participant_id = p.id
                SET p.archer_signature_data_url = s.archer_signature_data_url,
                    p.marker_signature_data_url = s.marker_signature_data_url;
                """);

            migrationBuilder.DropTable(
                name: "match_participant_signatures");
        }
    }
}
