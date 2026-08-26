using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecWeb.Migrations
{
    /// <inheritdoc />
    public partial class AttachMeetingsToTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectTeamId",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ProjectTeamId",
                table: "Meetings",
                column: "ProjectTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_ProjectTeams_ProjectTeamId",
                table: "Meetings",
                column: "ProjectTeamId",
                principalTable: "ProjectTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_ProjectTeams_ProjectTeamId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_ProjectTeamId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "ProjectTeamId",
                table: "Meetings");
        }
    }
}
