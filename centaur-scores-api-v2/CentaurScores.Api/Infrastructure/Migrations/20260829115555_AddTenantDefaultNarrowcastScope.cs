using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantDefaultNarrowcastScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "default_narrowcast_scope",
                table: "tenants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_narrowcast_scope",
                table: "tenants");
        }
    }
}
