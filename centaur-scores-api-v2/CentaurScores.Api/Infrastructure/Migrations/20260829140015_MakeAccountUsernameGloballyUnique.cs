using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeAccountUsernameGloballyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the replacement tenant_id index BEFORE dropping the composite one below: the live
            // database has an undocumented FK_accounts_tenants_tenant_id foreign key (not modeled in EF)
            // that requires an index with tenant_id as its leading column at all times, and dropping the
            // composite index first would leave a gap with no such index, which MySQL rejects.
            migrationBuilder.CreateIndex(
                name: "ix_accounts_tenant_id",
                table: "accounts",
                column: "tenant_id");

            migrationBuilder.DropIndex(
                name: "ix_accounts_tenant_id_username",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_username",
                table: "accounts",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Same ordering concern as Up(): create the composite index (also tenant_id-leading) before
            // dropping ix_accounts_tenant_id, so the FK always has a supporting index.
            migrationBuilder.CreateIndex(
                name: "ix_accounts_tenant_id_username",
                table: "accounts",
                columns: new[] { "tenant_id", "username" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "ix_accounts_tenant_id",
                table: "accounts");

            migrationBuilder.DropIndex(
                name: "ix_accounts_username",
                table: "accounts");
        }
    }
}
