using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SecWeb.Data;
using SecWeb.Data.Models;

namespace SecWeb.Services
{
    public class MeetingService
    {
        private readonly ApplicationDbContext _db;

        private readonly MeetingFileStorageService _fileStorage;


        public MeetingService(
            ApplicationDbContext db,
            MeetingFileStorageService fileStorage)
        {
            _db = db;

            _fileStorage = fileStorage;
        }


        // =========================================================
        // MEETING VISIBILITY
        // =========================================================
        //
        // Admin:
        //     Can view every meeting.
        //
        // Everyone else:
        //     Can view meetings belonging to a team they have
        //     currently joined.
        // =========================================================

        public async Task<List<Meeting>>
            GetMeetingsAsync(
                string userId,
                bool isAdmin)
        {
            IQueryable<Meeting> query =
                _db.Meetings
                    .AsNoTracking();


            if (!isAdmin)
            {
                query =
                    query.Where(
                        meeting =>
                            meeting.ProjectTeamId.HasValue &&

                            _db.ProjectMemberships.Any(
                                membership =>
                                    membership.ProjectTeamId ==
                                        meeting.ProjectTeamId.Value &&

                                    membership.UserId ==
                                        userId));
            }


            return await query
                .Include(meeting =>
                    meeting.Segments)

                .Include(meeting =>
                    meeting.Team)

                    .ThenInclude(team =>
                        team!.Project)

                .OrderByDescending(meeting =>
                    meeting.MeetingDate)

                .ThenBy(meeting =>
                    meeting.Title)

                .ToListAsync();
        }


        public async Task<Meeting?>
            GetMeetingAsync(
                int meetingId,
                string userId,
                bool isAdmin)
        {
            IQueryable<Meeting> query =
                _db.Meetings
                    .AsNoTracking()
                    .AsSplitQuery();


            if (!isAdmin)
            {
                query =
                    query.Where(
                        meeting =>
                            meeting.ProjectTeamId.HasValue &&

                            _db.ProjectMemberships.Any(
                                membership =>
                                    membership.ProjectTeamId ==
                                        meeting.ProjectTeamId.Value &&

                                    membership.UserId ==
                                        userId));
            }


            return await query
                .Include(meeting =>
                    meeting.Segments)

                .Include(meeting =>
                    meeting.Team)

                    .ThenInclude(team =>
                        team!.Project)

                .Include(meeting =>
                    meeting.TaskLists)

                    .ThenInclude(taskList =>
                        taskList.AssignedUser)

                .Include(meeting =>
                    meeting.TaskLists)

                    .ThenInclude(taskList =>
                        taskList.Tasks)

                        .ThenInclude(task =>
                            task.Files)

                .FirstOrDefaultAsync(
                    meeting =>
                        meeting.Id ==
                            meetingId);
        }


        // =========================================================
        // LEAD / ADMIN PERMISSIONS
        // =========================================================

        public async Task<bool>
            CanCreateMeetingAsync(
                string userId,
                bool isAdmin)
        {
            if (isAdmin)
            {
                return true;
            }


            return await _db.ProjectMemberships
                .AsNoTracking()

                .AnyAsync(
                    membership =>
                        membership.UserId ==
                            userId &&

                        membership.Role ==
                            ProjectRole.Lead);
        }


        public async Task<bool>
            CanManageMeetingAsync(
                int meetingId,
                string userId,
                bool isAdmin)
        {
            if (isAdmin)
            {
                return await _db.Meetings
                    .AsNoTracking()
                    .AnyAsync(
                        meeting =>
                            meeting.Id ==
                                meetingId);
            }


            int? teamId =
                await _db.Meetings
                    .AsNoTracking()

                    .Where(meeting =>
                        meeting.Id ==
                            meetingId)

                    .Select(meeting =>
                        meeting.ProjectTeamId)

                    .FirstOrDefaultAsync();


            if (!teamId.HasValue)
            {
                return false;
            }


            return await IsTeamManageableAsync(
                teamId.Value,
                userId,
                isAdmin: false);
        }


        public async Task<List<ProjectTeam>>
            GetManageableTeamsAsync(
                string userId,
                bool isAdmin)
        {
            IQueryable<ProjectTeam> query =
                _db.ProjectTeams
                    .AsNoTracking()

                    .Include(team =>
                        team.Project);


            if (!isAdmin)
            {
                query =
                    query.Where(
                        team =>
                            _db.ProjectMemberships.Any(
                                membership =>
                                    membership.ProjectTeamId ==
                                        team.Id &&

                                    membership.UserId ==
                                        userId &&

                                    membership.Role ==
                                        ProjectRole.Lead));
            }


            return await query
                .OrderBy(team =>
                    team.Project!.Name)

                .ThenBy(team =>
                    team.Name)

                .ToListAsync();
        }


        public async Task<List<ApplicationUser>>
            GetTeamMembersAsync(
                int teamId)
        {
            return await _db.ProjectMemberships
                .AsNoTracking()

                .Where(membership =>
                    membership.ProjectTeamId ==
                        teamId)

                .Include(membership =>
                    membership.User)

                .Where(membership =>
                    membership.User != null)

                .Select(membership =>
                    membership.User!)

                .OrderBy(user =>
                    user.LastName)

                .ThenBy(user =>
                    user.FirstName)

                .ThenBy(user =>
                    user.Email)

                .ToListAsync();
        }


        // =========================================================
        // CREATE MEETING
        // =========================================================

        public async Task<Meeting>
            CreateMeetingAsync(
                Meeting meeting,
                string userId,
                bool isAdmin)
        {
            if (!meeting.ProjectTeamId.HasValue)
            {
                throw new InvalidOperationException(
                    "Select a project team or subsystem.");
            }


            bool canManageTeam =
                await IsTeamManageableAsync(
                    meeting.ProjectTeamId.Value,
                    userId,
                    isAdmin);


            if (!canManageTeam)
            {
                throw new UnauthorizedAccessException(
                    "Only the Lead of that team or an Admin can create a meeting for it.");
            }


            meeting.UserId =
                userId;


            meeting.CreatedAt =
                DateTime.UtcNow;


            meeting.UpdatedAt =
                DateTime.UtcNow;


            meeting.Team =
                null;


            _db.Meetings.Add(
                meeting);


            await _db.SaveChangesAsync();


            return meeting;
        }


        // =========================================================
        // UPDATE MEETING
        // =========================================================

        public async Task UpdateMeetingAsync(
            Meeting meeting,
            string userId,
            bool isAdmin)
        {
            Meeting? existingMeeting =
                await _db.Meetings
                    .Include(existing =>
                        existing.Segments)

                    .FirstOrDefaultAsync(
                        existing =>
                            existing.Id ==
                                meeting.Id);


            if (existingMeeting == null)
            {
                throw new InvalidOperationException(
                    "The meeting could not be found.");
            }


            bool canManageCurrentMeeting =
                await CanManageMeetingAsync(
                    existingMeeting.Id,
                    userId,
                    isAdmin);


            if (!canManageCurrentMeeting)
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can edit this meeting.");
            }


            if (!meeting.ProjectTeamId.HasValue)
            {
                throw new InvalidOperationException(
                    "Select a project team or subsystem.");
            }


            bool canManageSelectedTeam =
                await IsTeamManageableAsync(
                    meeting.ProjectTeamId.Value,
                    userId,
                    isAdmin);


            if (!canManageSelectedTeam)
            {
                throw new UnauthorizedAccessException(
                    "You can only move a meeting to a team that you Lead.");
            }


            existingMeeting.ProjectTeamId =
                meeting.ProjectTeamId;


            existingMeeting.Title =
                meeting.Title.Trim();


            existingMeeting.MeetingDate =
                meeting.MeetingDate;


            existingMeeting.Description =
                meeting.Description;


            existingMeeting.UpdatedAt =
                DateTime.UtcNow;


            List<MeetingSegment> removedSegments =
                existingMeeting.Segments
                    .Where(existingSegment =>
                        !meeting.Segments.Any(
                            incomingSegment =>
                                incomingSegment.Id ==
                                    existingSegment.Id))
                    .ToList();


            foreach (
                MeetingSegment segment
                in removedSegments)
            {
                _db.MeetingSegments.Remove(
                    segment);
            }


            foreach (
                MeetingSegment incomingSegment
                in meeting.Segments)
            {
                if (incomingSegment.Id == 0)
                {
                    existingMeeting.Segments.Add(
                        new MeetingSegment
                        {
                            Title =
                                incomingSegment.Title,

                            StartTime =
                                incomingSegment.StartTime,

                            EndTime =
                                incomingSegment.EndTime,

                            Notes =
                                incomingSegment.Notes,

                            SortOrder =
                                incomingSegment.SortOrder
                        });


                    continue;
                }


                MeetingSegment? existingSegment =
                    existingMeeting.Segments
                        .FirstOrDefault(
                            segment =>
                                segment.Id ==
                                    incomingSegment.Id);


                if (existingSegment == null)
                {
                    continue;
                }


                existingSegment.Title =
                    incomingSegment.Title;


                existingSegment.StartTime =
                    incomingSegment.StartTime;


                existingSegment.EndTime =
                    incomingSegment.EndTime;


                existingSegment.Notes =
                    incomingSegment.Notes;


                existingSegment.SortOrder =
                    incomingSegment.SortOrder;
            }


            await _db.SaveChangesAsync();
        }


        // =========================================================
        // DELETE MEETING
        // =========================================================

        public async Task DeleteMeetingAsync(
            int meetingId,
            string userId,
            bool isAdmin)
        {
            Meeting? meeting =
                await _db.Meetings
                    .AsSplitQuery()

                    .Include(meeting =>
                        meeting.TaskLists)

                        .ThenInclude(taskList =>
                            taskList.Tasks)

                            .ThenInclude(task =>
                                task.Files)

                    .FirstOrDefaultAsync(
                        meeting =>
                            meeting.Id ==
                                meetingId);


            if (meeting == null)
            {
                return;
            }


            if (!await CanManageMeetingAsync(
                meetingId,
                userId,
                isAdmin))
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can delete this meeting.");
            }


            List<string> filesToDelete =
                meeting.TaskLists
                    .SelectMany(taskList =>
                        taskList.Tasks)

                    .SelectMany(task =>
                        task.Files)

                    .Select(file =>
                        file.StoredFileName)

                    .ToList();


            _db.Meetings.Remove(
                meeting);


            await _db.SaveChangesAsync();


            await DeletePhysicalFilesAsync(
                filesToDelete);
        }


        // =========================================================
        // MEMBER TASK TABLES
        // =========================================================

        public async Task<MeetingTaskList>
            AddTaskListAsync(
                int meetingId,
                string assignedUserId,
                string requestingUserId,
                bool isAdmin)
        {
            Meeting? meeting =
                await _db.Meetings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        meeting =>
                            meeting.Id ==
                                meetingId);


            if (meeting == null ||
                !meeting.ProjectTeamId.HasValue)
            {
                throw new InvalidOperationException(
                    "The meeting or its team could not be found.");
            }


            if (!await CanManageMeetingAsync(
                meetingId,
                requestingUserId,
                isAdmin))
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can delegate meeting tasks.");
            }


            bool isMember =
                await _db.ProjectMemberships
                    .AsNoTracking()

                    .AnyAsync(
                        membership =>
                            membership.ProjectTeamId ==
                                meeting.ProjectTeamId.Value &&

                            membership.UserId ==
                                assignedUserId);


            if (!isMember)
            {
                throw new InvalidOperationException(
                    "The selected user is not a member of this team.");
            }


            MeetingTaskList? existing =
                await _db.MeetingTaskLists
                    .FirstOrDefaultAsync(
                        taskList =>
                            taskList.MeetingId ==
                                meetingId &&

                            taskList.AssignedUserId ==
                                assignedUserId);


            if (existing != null)
            {
                return existing;
            }


            int nextSortOrder =
                await _db.MeetingTaskLists
                    .Where(taskList =>
                        taskList.MeetingId ==
                            meetingId)

                    .Select(taskList =>
                        (int?)taskList.SortOrder)

                    .MaxAsync() ?? -1;


            MeetingTaskList newTaskList =
                new()
                {
                    MeetingId =
                        meetingId,

                    AssignedUserId =
                        assignedUserId,

                    SortOrder =
                        nextSortOrder + 1
                };


            _db.MeetingTaskLists.Add(
                newTaskList);


            await _db.SaveChangesAsync();


            return newTaskList;
        }


        public async Task DeleteTaskListAsync(
            int taskListId,
            string requestingUserId,
            bool isAdmin)
        {
            MeetingTaskList? taskList =
                await _db.MeetingTaskLists
                    .AsSplitQuery()

                    .Include(taskList =>
                        taskList.Meeting)

                    .Include(taskList =>
                        taskList.Tasks)

                        .ThenInclude(task =>
                            task.Files)

                    .FirstOrDefaultAsync(
                        taskList =>
                            taskList.Id ==
                                taskListId);


            if (taskList?.Meeting == null)
            {
                return;
            }


            if (!await CanManageMeetingAsync(
                taskList.MeetingId,
                requestingUserId,
                isAdmin))
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can delete task tables.");
            }


            List<string> filesToDelete =
                taskList.Tasks
                    .SelectMany(task =>
                        task.Files)

                    .Select(file =>
                        file.StoredFileName)

                    .ToList();


            _db.MeetingTaskLists.Remove(
                taskList);


            await _db.SaveChangesAsync();


            await DeletePhysicalFilesAsync(
                filesToDelete);
        }


        // =========================================================
        // TASKS
        // =========================================================

        public async Task<MeetingTask>
            AddTaskAsync(
                int taskListId,
                string name,
                string? details,
                IBrowserFile? leadAttachment,
                string requestingUserId,
                bool isAdmin)
        {
            string cleanedName =
                name.Trim();


            if (string.IsNullOrWhiteSpace(
                cleanedName))
            {
                throw new InvalidOperationException(
                    "Enter a task name.");
            }


            MeetingTaskList? taskList =
                await _db.MeetingTaskLists
                    .AsNoTracking()

                    .Include(taskList =>
                        taskList.Meeting)

                    .FirstOrDefaultAsync(
                        taskList =>
                            taskList.Id ==
                                taskListId);


            if (taskList?.Meeting == null)
            {
                throw new InvalidOperationException(
                    "The task table could not be found.");
            }


            if (!await CanManageMeetingAsync(
                taskList.MeetingId,
                requestingUserId,
                isAdmin))
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can add tasks.");
            }


            int nextSortOrder =
                await _db.MeetingTasks
                    .Where(task =>
                        task.MeetingTaskListId ==
                            taskListId)

                    .Select(task =>
                        (int?)task.SortOrder)

                    .MaxAsync() ?? -1;


            StoredMeetingFile? storedAttachment =
                null;


            if (leadAttachment != null)
            {
                storedAttachment =
                    await _fileStorage.SaveAsync(
                        leadAttachment);
            }


            MeetingTask task =
                new()
                {
                    MeetingTaskListId =
                        taskListId,

                    Name =
                        cleanedName,

                    Details =
                        string.IsNullOrWhiteSpace(details)
                            ? null
                            : details.Trim(),

                    SortOrder =
                        nextSortOrder + 1,

                    CreatedAtUtc =
                        DateTimeOffset.UtcNow,

                    UpdatedAtUtc =
                        DateTimeOffset.UtcNow
                };


            if (storedAttachment != null)
            {
                task.Files.Add(
                    CreateFileEntity(
                        MeetingTaskFileKind.LeadAttachment,
                        storedAttachment));
            }


            _db.MeetingTasks.Add(
                task);


            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                if (storedAttachment != null)
                {
                    await _fileStorage.DeleteAsync(
                        storedAttachment.StoredFileName);
                }


                throw;
            }


            return task;
        }


        public async Task UpdateTaskAsync(
            int taskId,
            string name,
            string? details,
            IBrowserFile? replacementLeadAttachment,
            bool removeLeadAttachment,
            string requestingUserId,
            bool isAdmin)
        {
            string cleanedName =
                name.Trim();


            if (string.IsNullOrWhiteSpace(
                cleanedName))
            {
                throw new InvalidOperationException(
                    "Enter a task name.");
            }


            MeetingTask? task =
                await _db.MeetingTasks
                    .Include(task =>
                        task.Files)

                    .Include(task =>
                        task.TaskList)

                        .ThenInclude(taskList =>
                            taskList!.Meeting)

                    .FirstOrDefaultAsync(
                        task =>
                            task.Id ==
                                taskId);


            if (task?.TaskList?.Meeting == null)
            {
                throw new InvalidOperationException(
                    "The task could not be found.");
            }


            if (!await CanManageMeetingAsync(
                task.TaskList.MeetingId,
                requestingUserId,
                isAdmin))
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can edit tasks.");
            }


            MeetingTaskFile? currentLeadFile =
                task.Files.FirstOrDefault(
                    file =>
                        file.Kind ==
                            MeetingTaskFileKind.LeadAttachment);


            string? oldPhysicalFile =
                null;


            StoredMeetingFile? newStoredFile =
                null;


            if (replacementLeadAttachment != null)
            {
                newStoredFile =
                    await _fileStorage.SaveAsync(
                        replacementLeadAttachment);


                if (currentLeadFile == null)
                {
                    currentLeadFile =
                        CreateFileEntity(
                            MeetingTaskFileKind.LeadAttachment,
                            newStoredFile);


                    task.Files.Add(
                        currentLeadFile);
                }
                else
                {
                    oldPhysicalFile =
                        currentLeadFile.StoredFileName;


                    ApplyStoredFile(
                        currentLeadFile,
                        newStoredFile);
                }
            }
            else if (removeLeadAttachment &&
                     currentLeadFile != null)
            {
                oldPhysicalFile =
                    currentLeadFile.StoredFileName;


                _db.MeetingTaskFiles.Remove(
                    currentLeadFile);
            }


            task.Name =
                cleanedName;


            task.Details =
                string.IsNullOrWhiteSpace(details)
                    ? null
                    : details.Trim();


            task.UpdatedAtUtc =
                DateTimeOffset.UtcNow;


            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                if (newStoredFile != null)
                {
                    await _fileStorage.DeleteAsync(
                        newStoredFile.StoredFileName);
                }


                throw;
            }


            if (!string.IsNullOrWhiteSpace(
                oldPhysicalFile))
            {
                await _fileStorage.DeleteAsync(
                    oldPhysicalFile);
            }
        }


        public async Task DeleteTaskAsync(
            int taskId,
            string requestingUserId,
            bool isAdmin)
        {
            MeetingTask? task =
                await _db.MeetingTasks
                    .Include(task =>
                        task.Files)

                    .Include(task =>
                        task.TaskList)

                        .ThenInclude(taskList =>
                            taskList!.Meeting)

                    .FirstOrDefaultAsync(
                        task =>
                            task.Id ==
                                taskId);


            if (task?.TaskList?.Meeting == null)
            {
                return;
            }


            if (!await CanManageMeetingAsync(
                task.TaskList.MeetingId,
                requestingUserId,
                isAdmin))
            {
                throw new UnauthorizedAccessException(
                    "Only the team Lead or an Admin can delete tasks.");
            }


            List<string> filesToDelete =
                task.Files
                    .Select(file =>
                        file.StoredFileName)
                    .ToList();


            _db.MeetingTasks.Remove(
                task);


            await _db.SaveChangesAsync();


            await DeletePhysicalFilesAsync(
                filesToDelete);
        }


        // =========================================================
        // MEMBER TASK SUBMISSION
        // =========================================================

        public async Task SubmitTaskAsync(
            int taskId,
            string assignedUserId,
            string? comments,
            IBrowserFile? submissionFile)
        {
            MeetingTask? task =
                await _db.MeetingTasks
                    .Include(task =>
                        task.Files)

                    .Include(task =>
                        task.TaskList)

                        .ThenInclude(taskList =>
                            taskList!.Meeting)

                    .FirstOrDefaultAsync(
                        task =>
                            task.Id ==
                                taskId);


            if (task?.TaskList?.Meeting == null)
            {
                throw new InvalidOperationException(
                    "The task could not be found.");
            }


            if (task.TaskList.AssignedUserId !=
                assignedUserId)
            {
                throw new UnauthorizedAccessException(
                    "Only the member assigned to this task can submit work for it.");
            }


            Meeting meeting =
                task.TaskList.Meeting;


            if (!meeting.ProjectTeamId.HasValue)
            {
                throw new InvalidOperationException(
                    "This meeting is not attached to a team.");
            }


            bool isCurrentTeamMember =
                await _db.ProjectMemberships
                    .AsNoTracking()

                    .AnyAsync(
                        membership =>
                            membership.ProjectTeamId ==
                                meeting.ProjectTeamId.Value &&

                            membership.UserId ==
                                assignedUserId);


            if (!isCurrentTeamMember)
            {
                throw new UnauthorizedAccessException(
                    "You are no longer a member of the team for this meeting.");
            }


            MeetingTaskFile? existingSubmissionFile =
                task.Files.FirstOrDefault(
                    file =>
                        file.Kind ==
                            MeetingTaskFileKind.Submission);


            bool hasComments =
                !string.IsNullOrWhiteSpace(
                    comments);


            if (!hasComments &&
                submissionFile == null &&
                existingSubmissionFile == null)
            {
                throw new InvalidOperationException(
                    "Add a file, comments, or both before submitting.");
            }


            string? oldPhysicalFile =
                null;


            StoredMeetingFile? newStoredFile =
                null;


            if (submissionFile != null)
            {
                newStoredFile =
                    await _fileStorage.SaveAsync(
                        submissionFile);


                if (existingSubmissionFile == null)
                {
                    existingSubmissionFile =
                        CreateFileEntity(
                            MeetingTaskFileKind.Submission,
                            newStoredFile);


                    task.Files.Add(
                        existingSubmissionFile);
                }
                else
                {
                    oldPhysicalFile =
                        existingSubmissionFile.StoredFileName;


                    ApplyStoredFile(
                        existingSubmissionFile,
                        newStoredFile);
                }
            }


            task.SubmissionComments =
                string.IsNullOrWhiteSpace(comments)
                    ? null
                    : comments.Trim();


            task.SubmittedAtUtc =
                DateTimeOffset.UtcNow;


            task.UpdatedAtUtc =
                DateTimeOffset.UtcNow;


            try
            {
                await _db.SaveChangesAsync();
            }
            catch
            {
                if (newStoredFile != null)
                {
                    await _fileStorage.DeleteAsync(
                        newStoredFile.StoredFileName);
                }


                throw;
            }


            if (!string.IsNullOrWhiteSpace(
                oldPhysicalFile))
            {
                await _fileStorage.DeleteAsync(
                    oldPhysicalFile);
            }
        }


        // =========================================================
        // INTERNAL HELPERS
        // =========================================================

        private async Task<bool>
            IsTeamManageableAsync(
                int teamId,
                string userId,
                bool isAdmin)
        {
            if (isAdmin)
            {
                return await _db.ProjectTeams
                    .AsNoTracking()
                    .AnyAsync(
                        team =>
                            team.Id ==
                                teamId);
            }


            return await _db.ProjectMemberships
                .AsNoTracking()

                .AnyAsync(
                    membership =>
                        membership.ProjectTeamId ==
                            teamId &&

                        membership.UserId ==
                            userId &&

                        membership.Role ==
                            ProjectRole.Lead);
        }


        private static MeetingTaskFile
            CreateFileEntity(
                MeetingTaskFileKind kind,
                StoredMeetingFile storedFile)
        {
            return new MeetingTaskFile
            {
                Kind =
                    kind,

                OriginalFileName =
                    storedFile.OriginalFileName,

                StoredFileName =
                    storedFile.StoredFileName,

                ContentType =
                    storedFile.ContentType,

                SizeBytes =
                    storedFile.SizeBytes,

                UploadedAtUtc =
                    DateTimeOffset.UtcNow
            };
        }


        private static void ApplyStoredFile(
            MeetingTaskFile target,
            StoredMeetingFile storedFile)
        {
            target.OriginalFileName =
                storedFile.OriginalFileName;


            target.StoredFileName =
                storedFile.StoredFileName;


            target.ContentType =
                storedFile.ContentType;


            target.SizeBytes =
                storedFile.SizeBytes;


            target.UploadedAtUtc =
                DateTimeOffset.UtcNow;
        }


        private async Task DeletePhysicalFilesAsync(
            IEnumerable<string> storedFileNames)
        {
            foreach (
                string storedFileName
                in storedFileNames.Distinct())
            {
                await _fileStorage.DeleteAsync(
                    storedFileName);
            }
        }
    }
}
