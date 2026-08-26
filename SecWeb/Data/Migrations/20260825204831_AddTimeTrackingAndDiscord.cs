using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeTrackingAndDiscord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClubWide",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DiscordAvatarHash",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordDisplayName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscordLinkedAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUserId",
                table: "AspNetUsers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUsername",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ProjectNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activity = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    DiscordGuildId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DiscordChannelId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastEditedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LastEditedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkLogs_AspNetUsers_LastEditedByUserId",
                        column: x => x.LastEditedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkLogAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkLogId = table.Column<int>(type: "int", nullable: false),
                    EditedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreviousProjectId = table.Column<int>(type: "int", nullable: true),
                    PreviousProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NewProjectId = table.Column<int>(type: "int", nullable: true),
                    NewProjectName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PreviousActivity = table.Column<int>(type: "int", nullable: false),
                    NewActivity = table.Column<int>(type: "int", nullable: false),
                    PreviousStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PreviousEndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NewStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NewEndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkLogAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkLogAudits_AspNetUsers_EditedByUserId",
                        column: x => x.EditedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkLogAudits_WorkLogs_WorkLogId",
                        column: x => x.WorkLogId,
                        principalTable: "WorkLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DiscordUserId",
                table: "AspNetUsers",
                column: "DiscordUserId",
                unique: true,
                filter: "[DiscordUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogAudits_EditedByUserId",
                table: "WorkLogAudits",
                column: "EditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogAudits_WorkLogId",
                table: "WorkLogAudits",
                column: "WorkLogId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_EndedAtUtc",
                table: "WorkLogs",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_LastEditedByUserId",
                table: "WorkLogs",
                column: "LastEditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_ProjectId",
                table: "WorkLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_StartedAtUtc",
                table: "WorkLogs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkLogs_UserId",
                table: "WorkLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkLogAudits");

            migrationBuilder.DropTable(
                name: "WorkLogs");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Name",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DiscordUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsClubWide",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsProtected",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DiscordAvatarHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DiscordDisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DiscordLinkedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DiscordUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DiscordUsername",
                table: "AspNetUsers");
        }
    }
}
