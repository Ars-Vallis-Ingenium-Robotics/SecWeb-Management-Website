using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecWeb.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMeetingTaskSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingTaskLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeetingId = table.Column<int>(type: "int", nullable: false),
                    AssignedUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTaskLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingTaskLists_AspNetUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingTaskLists_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeetingTaskListId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmissionComments = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingTasks_MeetingTaskLists_MeetingTaskListId",
                        column: x => x.MeetingTaskListId,
                        principalTable: "MeetingTaskLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingTaskFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeetingTaskId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTaskFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingTaskFiles_MeetingTasks_MeetingTaskId",
                        column: x => x.MeetingTaskId,
                        principalTable: "MeetingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTaskFiles_MeetingTaskId_Kind",
                table: "MeetingTaskFiles",
                columns: new[] { "MeetingTaskId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTaskLists_AssignedUserId",
                table: "MeetingTaskLists",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTaskLists_MeetingId_AssignedUserId",
                table: "MeetingTaskLists",
                columns: new[] { "MeetingId", "AssignedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTasks_MeetingTaskListId",
                table: "MeetingTasks",
                column: "MeetingTaskListId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingTaskFiles");

            migrationBuilder.DropTable(
                name: "MeetingTasks");

            migrationBuilder.DropTable(
                name: "MeetingTaskLists");
        }
    }
}
