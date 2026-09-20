using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScorecardSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "signature_mode",
                table: "matches",
                type: "longtext",
                nullable: false,
                defaultValue: "none")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "signature_mode",
                table: "match_templates",
                type: "longtext",
                nullable: false,
                defaultValue: "none")
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.AddColumn<bool>(
                name: "signed",
                table: "match_participants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "signed_at_utc",
                table: "match_participants",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "signature_mode",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "signature_mode",
                table: "match_templates");

            migrationBuilder.DropColumn(
                name: "archer_signature_data_url",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "marker_signature_data_url",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "signed",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "signed_at_utc",
                table: "match_participants");
        }
    }
}
