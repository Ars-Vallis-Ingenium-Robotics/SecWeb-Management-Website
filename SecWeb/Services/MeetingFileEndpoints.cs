using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SecWeb.Data;
using SecWeb.Data.Models;

namespace SecWeb.Services
{
    public static class MeetingFileEndpoints
    {
        public static IEndpointRouteBuilder
            MapMeetingFileEndpoints(
                this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet(
                "/meeting-task-files/{fileId:int}",
                DownloadMeetingTaskFileAsync)
                .RequireAuthorization();


            return endpoints;
        }


        private static async Task<IResult>
            DownloadMeetingTaskFileAsync(
                int fileId,
                HttpContext context,
                ApplicationDbContext db,
                MeetingFileStorageService storage)
        {
            string? userId =
                context.User
                    .FindFirstValue(
                        ClaimTypes.NameIdentifier);


            if (string.IsNullOrWhiteSpace(
                userId))
            {
                return Results.Unauthorized();
            }


            MeetingTaskFile? file =
                await db.MeetingTaskFiles
                    .AsNoTracking()

                    .Include(file =>
                        file.MeetingTask)

                        .ThenInclude(task =>
                            task!.TaskList)

                            .ThenInclude(taskList =>
                                taskList!.Meeting)

                    .FirstOrDefaultAsync(
                        file =>
                            file.Id == fileId);


            if (file?.MeetingTask?.TaskList?.Meeting == null)
            {
                return Results.NotFound();
            }


            MeetingTask task =
                file.MeetingTask;


            MeetingTaskList taskList =
                task.TaskList!;


            Meeting meeting =
                taskList.Meeting!;


            bool isAdmin =
                context.User.IsInRole(
                    AccountRoles.Admin);


            bool isTeamMember =
                false;


            bool isTeamLead =
                false;


            if (meeting.ProjectTeamId.HasValue)
            {
                ProjectMembership? membership =
                    await db.ProjectMemberships
                        .AsNoTracking()

                        .FirstOrDefaultAsync(
                            membership =>
                                membership.ProjectTeamId ==
                                    meeting.ProjectTeamId.Value &&

                                membership.UserId ==
                                    userId);


                if (membership != null)
                {
                    isTeamMember =
                        true;


                    isTeamLead =
                        membership.Role ==
                            ProjectRole.Lead;
                }
            }


            if (!isAdmin &&
                !isTeamMember)
            {
                return Results.Forbid();
            }


            // Lead attachments can be downloaded by anyone who can
            // view the team meeting.
            //
            // Submission files are limited to the assigned member,
            // that team's Lead, and Admins.

            if (file.Kind ==
                    MeetingTaskFileKind.Submission &&

                !isAdmin &&
                !isTeamLead &&
                taskList.AssignedUserId !=
                    userId)
            {
                return Results.Forbid();
            }


            string physicalPath =
                storage.GetPhysicalPath(
                    file.StoredFileName);


            if (!File.Exists(
                physicalPath))
            {
                return Results.NotFound();
            }


            context.Response.Headers[
                "X-Content-Type-Options"] =
                    "nosniff";


            string contentType =
                string.IsNullOrWhiteSpace(
                    file.ContentType)

                    ? "application/octet-stream"

                    : file.ContentType;


            return Results.File(
                physicalPath,
                contentType,
                file.OriginalFileName,
                enableRangeProcessing: true);
        }
    }
}
