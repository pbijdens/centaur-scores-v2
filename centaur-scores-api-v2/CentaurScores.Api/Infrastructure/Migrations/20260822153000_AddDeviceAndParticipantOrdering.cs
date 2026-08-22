using CentaurScores.Api.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260822153000_AddDeviceAndParticipantOrdering")]
    public partial class AddDeviceAndParticipantOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int?>(
                name: "device_order",
                table: "match_participants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "score_devices",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "device_order",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "score_devices");
        }
    }
}
