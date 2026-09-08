using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentaurScores.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchParticipantListMemberForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_match_participants_participant_list_member_id",
                table: "match_participants",
                column: "participant_list_member_id");

            migrationBuilder.AddForeignKey(
                name: "fk_match_participants_participant_list_members_participant_list",
                table: "match_participants",
                column: "participant_list_member_id",
                principalTable: "participant_list_members",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_match_participants_participant_list_members_participant_list",
                table: "match_participants");

            migrationBuilder.DropIndex(
                name: "ix_match_participants_participant_list_member_id",
                table: "match_participants");
        }
    }
}
