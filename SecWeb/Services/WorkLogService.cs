using Microsoft.EntityFrameworkCore;
using SecWeb.Data;
using SecWeb.Data.Models;

namespace SecWeb.Services
{
    public class WorkLogService
    {
        private readonly ApplicationDbContext _db;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public WorkLogService(
            ApplicationDbContext db)
        {
            _db = db;
        }


        // =========================================================
        // FIND SECWEB USER FROM DISCORD
        // =========================================================

        public async Task<ApplicationUser?>
            GetUserByDiscordIdAsync(
                ulong discordUserId)
        {
            string discordId =
                discordUserId.ToString();


            return await _db.Users
                .AsNoTracking()

                .FirstOrDefaultAsync(
                    user =>
                        user.DiscordUserId ==
                        discordId);
        }


        // =========================================================
        // GET PROJECTS USER CAN LOG TIME TO
        // =========================================================

        public async Task<List<Project>>
            GetLoggableProjectsAsync(
                string userId)
        {
            return await _db.Projects
                .AsNoTracking()

                .Where(
                    project =>

                        project.IsClubWide ||

                        _db.ProjectMemberships.Any(
                            membership =>
                                membership.ProjectId ==
                                    project.Id &&

                                membership.UserId ==
                                    userId))

                .OrderBy(
                    project =>
                        project.Name)

                .ToListAsync();
        }


        // =========================================================
        // GET ALL PROJECTS
        // =========================================================

        public async Task<List<Project>>
            GetAllProjectsAsync()
        {
            return await _db.Projects
                .AsNoTracking()

                .OrderBy(
                    project =>
                        project.Name)

                .ToListAsync();
        }


        // =========================================================
        // GET PROJECTS AVAILABLE WHILE EDITING
        // =========================================================

        public async Task<List<Project>>
            GetProjectsForEditingAsync(
                string userId,
                bool isAdmin,
                int? currentProjectId)
        {
            IQueryable<Project> query =
                _db.Projects
                    .AsNoTracking();


            if (!isAdmin)
            {
                query =
                    query.Where(
                        project =>

                            project.IsClubWide ||

                            project.Id ==
                                currentProjectId ||

                            _db.ProjectMemberships.Any(
                                membership =>
                                    membership.ProjectId ==
                                        project.Id &&

                                    membership.UserId ==
                                        userId));
            }


            return await query
                .OrderBy(
                    project =>
                        project.Name)

                .ToListAsync();
        }


        // =========================================================
        // GET ACTIVE TIMERS
        // =========================================================

        public async Task<List<WorkLog>>
            GetActiveTimersAsync(
                string userId)
        {
            return await _db.WorkLogs
                .AsNoTracking()

                .Where(
                    log =>
                        log.UserId ==
                            userId &&

                        log.EndedAtUtc ==
                            null)

                .OrderBy(
                    log =>
                        log.StartedAtUtc)

                .ToListAsync();
        }


        // =========================================================
        // START DISCORD TIMER
        // =========================================================

        public async Task<WorkLog>
            StartTimerAsync(
                string userId,
                int projectId,
                WorkActivity activity,
                ulong discordGuildId,
                ulong discordChannelId)
        {
            Project? project =
                await _db.Projects

                    .FirstOrDefaultAsync(
                        project =>
                            project.Id ==
                            projectId);


            if (project == null)
            {
                throw new InvalidOperationException(
                    "That project no longer exists.");
            }


            bool userMayLogTime =
                await UserCanLogToProjectAsync(
                    userId,
                    project);


            if (!userMayLogTime)
            {
                throw new InvalidOperationException(
                    "You are not a member of that project.");
            }


            // -----------------------------------------------------
            // ONLY ONE ACTIVE TIMER
            // -----------------------------------------------------

            bool alreadyRunning =
                await _db.WorkLogs

                    .AnyAsync(
                        log =>
                            log.UserId ==
                                userId &&

                            log.EndedAtUtc ==
                                null);


            if (alreadyRunning)
            {
                throw new InvalidOperationException(
                    "You already have an active timer. Use !stop before starting another one.");
            }


            DateTimeOffset now =
                DateTimeOffset.UtcNow;


            WorkLog log =
                new()
                {
                    UserId =
                        userId,

                    ProjectId =
                        project.Id,

                    ProjectNameSnapshot =
                        project.Name,

                    Activity =
                        activity,

                    StartedAtUtc =
                        now,

                    EndedAtUtc =
                        null,

                    Source =
                        WorkLogSource.Discord,

                    DiscordGuildId =
                        discordGuildId.ToString(),

                    DiscordChannelId =
                        discordChannelId.ToString(),

                    CreatedAtUtc =
                        now,

                    UpdatedAtUtc =
                        now
                };


            _db.WorkLogs.Add(
                log);


            await _db.SaveChangesAsync();


            return log;
        }


        // =========================================================
        // CREATE MANUAL WEBSITE TIME
        // =========================================================

        public async Task<WorkLog>
            CreateManualWorkLogAsync(
                string userId,
                int projectId,
                WorkActivity activity,
                DateTimeOffset startedAtUtc,
                DateTimeOffset endedAtUtc)
        {
            // -----------------------------------------------------
            // USER MUST EXIST
            // -----------------------------------------------------

            bool userExists =
                await _db.Users

                    .AnyAsync(
                        user =>
                            user.Id ==
                            userId);


            if (!userExists)
            {
                throw new InvalidOperationException(
                    "The SecWeb user account could not be found.");
            }


            // -----------------------------------------------------
            // VALIDATE TIMES
            // -----------------------------------------------------

            if (endedAtUtc <=
                startedAtUtc)
            {
                throw new InvalidOperationException(
                    "The ending time must be later than the starting time.");
            }


            // -----------------------------------------------------
            // PROJECT MUST EXIST
            // -----------------------------------------------------

            Project? project =
                await _db.Projects

                    .FirstOrDefaultAsync(
                        project =>
                            project.Id ==
                            projectId);


            if (project == null)
            {
                throw new InvalidOperationException(
                    "The selected project does not exist.");
            }


            // -----------------------------------------------------
            // PROJECT ACCESS
            // -----------------------------------------------------

            bool canLog =
                await UserCanLogToProjectAsync(
                    userId,
                    project);


            if (!canLog)
            {
                throw new UnauthorizedAccessException(
                    "You cannot add time to a project you are not a member of.");
            }


            // -----------------------------------------------------
            // PREVENT OVERLAPPING TIME
            // -----------------------------------------------------

            await ValidateNoOverlapAsync(
                userId,
                startedAtUtc,
                endedAtUtc,
                ignoredWorkLogId: null);


            DateTimeOffset now =
                DateTimeOffset.UtcNow;


            // -----------------------------------------------------
            // CREATE WORK LOG
            // -----------------------------------------------------

            WorkLog workLog =
                new()
                {
                    UserId =
                        userId,

                    ProjectId =
                        project.Id,

                    ProjectNameSnapshot =
                        project.Name,

                    Activity =
                        activity,

                    StartedAtUtc =
                        startedAtUtc,

                    EndedAtUtc =
                        endedAtUtc,

                    Source =
                        WorkLogSource.Website,

                    DiscordGuildId =
                        null,

                    DiscordChannelId =
                        null,

                    CreatedAtUtc =
                        now,

                    UpdatedAtUtc =
                        now,

                    LastEditedByUserId =
                        null,

                    LastEditedAtUtc =
                        null
                };


            _db.WorkLogs.Add(
                workLog);


            await _db.SaveChangesAsync();


            return workLog;
        }


        // =========================================================
        // STOP ALL ACTIVE TIMERS
        // =========================================================

        public async Task<List<WorkLog>>
            StopAllTimersAsync(
                string userId)
        {
            List<WorkLog> activeTimers =
                await _db.WorkLogs

                    .Where(
                        log =>
                            log.UserId ==
                                userId &&

                            log.EndedAtUtc ==
                                null)

                    .OrderBy(
                        log =>
                            log.StartedAtUtc)

                    .ToListAsync();


            if (activeTimers.Count == 0)
            {
                return activeTimers;
            }


            DateTimeOffset stoppedAt =
                DateTimeOffset.UtcNow;


            foreach (
                WorkLog timer
                in activeTimers)
            {
                timer.EndedAtUtc =
                    stoppedAt;


                timer.UpdatedAtUtc =
                    stoppedAt;
            }


            await _db.SaveChangesAsync();


            return activeTimers;
        }


        // =========================================================
        // GET USER SUMMARY FOR CHIPY !TIME
        // =========================================================

        public async Task<WorkLogUserSummary>
            GetUserSummaryAsync(
                string userId)
        {
            List<WorkLog> logs =
                await _db.WorkLogs
                    .AsNoTracking()

                    .Where(
                        log =>
                            log.UserId ==
                            userId)

                    .OrderByDescending(
                        log =>
                            log.StartedAtUtc)

                    .ToListAsync();


            List<WorkLog> activeTimers =
                logs

                    .Where(
                        log =>
                            log.EndedAtUtc ==
                            null)

                    .OrderBy(
                        log =>
                            log.StartedAtUtc)

                    .ToList();


            List<WorkLog> completedLogs =
                logs

                    .Where(
                        log =>
                            log.EndedAtUtc !=
                            null)

                    .ToList();


            TimeSpan totalCompleted =
                CalculateCompletedTime(
                    completedLogs);


            List<WorkLog> recentLogs =
                completedLogs
                    .Take(5)
                    .ToList();


            return new WorkLogUserSummary(
                totalCompleted,
                activeTimers,
                recentLogs);
        }


        // =========================================================
        // PROJECT LEADERBOARD FOR CHIPY !ALL
        // =========================================================

        public async Task<
            List<ProjectLeaderboardEntry>>
            GetProjectLeaderboardAsync(
                int projectId)
        {
            List<WorkLog> completedLogs =
                await _db.WorkLogs
                    .AsNoTracking()

                    .Include(
                        log =>
                            log.User)

                    .Where(
                        log =>
                            log.ProjectId ==
                                projectId &&

                            log.EndedAtUtc !=
                                null)

                    .ToListAsync();


            List<ProjectLeaderboardEntry> results =
                completedLogs

                    .GroupBy(
                        log =>
                            log.UserId)

                    .Select(
                        group =>
                        {
                            WorkLog first =
                                group.First();


                            string displayName =
                                GetDisplayName(
                                    first.User);


                            TimeSpan total =
                                CalculateCompletedTime(
                                    group);


                            return new ProjectLeaderboardEntry(
                                group.Key,
                                displayName,
                                total);
                        })

                    .OrderByDescending(
                        entry =>
                            entry.TotalTime)

                    .ThenBy(
                        entry =>
                            entry.DisplayName)

                    .ToList();


            return results;
        }


        // =========================================================
        // GET WEBSITE LOGS
        // =========================================================

        public async Task<List<WorkLog>>
            GetWebsiteLogsAsync()
        {
            return await _db.WorkLogs
                .AsNoTracking()

                .Include(
                    log =>
                        log.User)

                .Include(
                    log =>
                        log.Project)

                .OrderByDescending(
                    log =>
                        log.StartedAtUtc)

                .ToListAsync();
        }


        // =========================================================
        // GET WEBSITE SUMMARY
        // =========================================================

        public async Task<WorkLogWebsiteSummary>
            GetWebsiteSummaryAsync(
                string currentUserId)
        {
            List<WorkLog> logs =
                await _db.WorkLogs
                    .AsNoTracking()
                    .ToListAsync();


            TimeSpan myCompletedTime =
                CalculateCompletedTime(
                    logs.Where(
                        log =>
                            log.UserId ==
                                currentUserId));


            TimeSpan overallCompletedTime =
                CalculateCompletedTime(
                    logs);


            int activeTimerCount =
                logs.Count(
                    log =>
                        log.EndedAtUtc ==
                            null);


            DateTimeOffset? mostRecent =
                logs

                    .OrderByDescending(
                        log =>
                            log.StartedAtUtc)

                    .Select(
                        log =>
                            (DateTimeOffset?)
                                log.StartedAtUtc)

                    .FirstOrDefault();


            return new WorkLogWebsiteSummary(
                myCompletedTime,
                overallCompletedTime,
                activeTimerCount,
                mostRecent);
        }


        // =========================================================
        // GET ONE LOG FOR EDITING
        // =========================================================

        public async Task<WorkLog?>
            GetWorkLogForEditAsync(
                int workLogId,
                string requestingUserId,
                bool isAdmin)
        {
            WorkLog? log =
                await _db.WorkLogs
                    .AsNoTracking()

                    .Include(
                        log =>
                            log.User)

                    .Include(
                        log =>
                            log.Project)

                    .FirstOrDefaultAsync(
                        log =>
                            log.Id ==
                            workLogId);


            if (log == null)
            {
                return null;
            }


            // Admin can edit any record.
            if (isAdmin)
            {
                return log;
            }


            // Normal users can only edit their own records.
            if (log.UserId !=
                requestingUserId)
            {
                return null;
            }


            return log;
        }


        // =========================================================
        // UPDATE WORK LOG
        // =========================================================

        public async Task<WorkLog>
            UpdateWorkLogAsync(
                WorkLogUpdateRequest request,
                string requestingUserId,
                bool isAdmin)
        {
            WorkLog? log =
                await _db.WorkLogs

                    .FirstOrDefaultAsync(
                        log =>
                            log.Id ==
                            request.WorkLogId);


            if (log == null)
            {
                throw new InvalidOperationException(
                    "The work log could not be found.");
            }


            // -----------------------------------------------------
            // AUTHORIZATION
            // -----------------------------------------------------

            if (!isAdmin &&
                log.UserId !=
                    requestingUserId)
            {
                throw new UnauthorizedAccessException(
                    "You can only edit your own time records.");
            }


            // -----------------------------------------------------
            // TIME VALIDATION
            // -----------------------------------------------------

            if (request.EndedAtUtc.HasValue &&
                request.EndedAtUtc.Value <=
                    request.StartedAtUtc)
            {
                throw new InvalidOperationException(
                    "The ending time must be later than the starting time.");
            }


            // -----------------------------------------------------
            // PROJECT
            // -----------------------------------------------------

            Project? selectedProject =
                null;


            string newProjectName =
                log.ProjectNameSnapshot;


            if (request.ProjectId.HasValue)
            {
                selectedProject =
                    await _db.Projects

                        .FirstOrDefaultAsync(
                            project =>
                                project.Id ==
                                request.ProjectId.Value);


                if (selectedProject == null)
                {
                    throw new InvalidOperationException(
                        "The selected project does not exist.");
                }


                if (!isAdmin &&
                    selectedProject.Id !=
                        log.ProjectId)
                {
                    bool allowed =
                        await UserCanLogToProjectAsync(
                            requestingUserId,
                            selectedProject);


                    if (!allowed)
                    {
                        throw new UnauthorizedAccessException(
                            "You cannot move this time record to a project you are not a member of.");
                    }
                }


                newProjectName =
                    selectedProject.Name;
            }
            else
            {
                if (log.ProjectId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Select a valid project.");
                }
            }


            // -----------------------------------------------------
            // OVERLAP VALIDATION
            // -----------------------------------------------------

            DateTimeOffset requestedEnd =
                request.EndedAtUtc ??
                DateTimeOffset.MaxValue;


            await ValidateNoOverlapAsync(
                log.UserId,
                request.StartedAtUtc,
                requestedEnd,
                log.Id);


            // -----------------------------------------------------
            // CREATE AUDIT ENTRY
            // -----------------------------------------------------

            WorkLogAudit audit =
                new()
                {
                    WorkLogId =
                        log.Id,

                    EditedByUserId =
                        requestingUserId,

                    EditedAtUtc =
                        DateTimeOffset.UtcNow,

                    PreviousProjectId =
                        log.ProjectId,

                    PreviousProjectName =
                        log.ProjectNameSnapshot,

                    NewProjectId =
                        selectedProject?.Id,

                    NewProjectName =
                        newProjectName,

                    PreviousActivity =
                        log.Activity,

                    NewActivity =
                        request.Activity,

                    PreviousStartedAtUtc =
                        log.StartedAtUtc,

                    PreviousEndedAtUtc =
                        log.EndedAtUtc,

                    NewStartedAtUtc =
                        request.StartedAtUtc,

                    NewEndedAtUtc =
                        request.EndedAtUtc
                };


            _db.WorkLogAudits.Add(
                audit);


            // -----------------------------------------------------
            // UPDATE RECORD
            // -----------------------------------------------------

            log.ProjectId =
                selectedProject?.Id;


            log.ProjectNameSnapshot =
                newProjectName;


            log.Activity =
                request.Activity;


            log.StartedAtUtc =
                request.StartedAtUtc;


            log.EndedAtUtc =
                request.EndedAtUtc;


            log.UpdatedAtUtc =
                DateTimeOffset.UtcNow;


            log.LastEditedByUserId =
                requestingUserId;


            log.LastEditedAtUtc =
                DateTimeOffset.UtcNow;


            await _db.SaveChangesAsync();


            return log;
        }


        // =========================================================
        // DELETE WORK LOG
        // =========================================================
        //
        // Normal users:
        //     Can delete only their own logs.
        //
        // Admin:
        //     Can delete any user's log.
        //
        // This authorization is enforced here in the service,
        // not only by the Time Tracking page.
        //

        public async Task DeleteWorkLogAsync(
            int workLogId,
            string requestingUserId,
            bool isAdmin)
        {
            WorkLog? log =
                await _db.WorkLogs

                    .Include(
                        log =>
                            log.AuditHistory)

                    .FirstOrDefaultAsync(
                        log =>
                            log.Id ==
                            workLogId);


            // -----------------------------------------------------
            // LOG MUST EXIST
            // -----------------------------------------------------

            if (log == null)
            {
                throw new InvalidOperationException(
                    "The work log could not be found.");
            }


            // -----------------------------------------------------
            // AUTHORIZATION
            // -----------------------------------------------------

            if (!isAdmin &&
                log.UserId !=
                    requestingUserId)
            {
                throw new UnauthorizedAccessException(
                    "You can only delete your own time records.");
            }


            // -----------------------------------------------------
            // DELETE AUDIT HISTORY
            // -----------------------------------------------------
            //
            // ApplicationDbContext currently configures audit
            // records to cascade when the WorkLog is deleted.
            //
            // Removing them explicitly here also makes the
            // intention clear and avoids leaving old tracking
            // records attached to a deleted WorkLog.
            //

            if (log.AuditHistory.Count > 0)
            {
                _db.WorkLogAudits.RemoveRange(
                    log.AuditHistory);
            }


            // -----------------------------------------------------
            // DELETE WORK LOG
            // -----------------------------------------------------

            _db.WorkLogs.Remove(
                log);


            await _db.SaveChangesAsync();
        }


        // =========================================================
        // PROJECT ACCESS CHECK
        // =========================================================

        private async Task<bool>
            UserCanLogToProjectAsync(
                string userId,
                Project project)
        {
            if (project.IsClubWide)
            {
                return true;
            }


            return await _db.ProjectMemberships

                .AnyAsync(
                    membership =>
                        membership.ProjectId ==
                            project.Id &&

                        membership.UserId ==
                            userId);
        }


        // =========================================================
        // OVERLAP VALIDATION
        // =========================================================

        private async Task ValidateNoOverlapAsync(
            string userId,
            DateTimeOffset start,
            DateTimeOffset end,
            int? ignoredWorkLogId)
        {
            List<WorkLog> existingLogs =
                await _db.WorkLogs
                    .AsNoTracking()

                    .Where(
                        log =>
                            log.UserId ==
                                userId &&

                            (!ignoredWorkLogId.HasValue ||
                             log.Id !=
                                ignoredWorkLogId.Value))

                    .ToListAsync();


            foreach (
                WorkLog existing
                in existingLogs)
            {
                DateTimeOffset existingEnd =
                    existing.EndedAtUtc ??
                    DateTimeOffset.MaxValue;


                bool overlaps =
                    start <
                        existingEnd &&

                    existing.StartedAtUtc <
                        end;


                if (overlaps)
                {
                    throw new InvalidOperationException(
                        "This time overlaps another work log on your account.");
                }
            }
        }


        // =========================================================
        // CALCULATE COMPLETED TIME
        // =========================================================

        private static TimeSpan CalculateCompletedTime(
            IEnumerable<WorkLog> logs)
        {
            TimeSpan total =
                TimeSpan.Zero;


            foreach (
                WorkLog log
                in logs)
            {
                if (!log.EndedAtUtc.HasValue)
                {
                    continue;
                }


                TimeSpan duration =
                    log.EndedAtUtc.Value -
                    log.StartedAtUtc;


                if (duration >
                    TimeSpan.Zero)
                {
                    total +=
                        duration;
                }
            }


            return total;
        }


        // =========================================================
        // DISPLAY NAME
        // =========================================================

        private static string GetDisplayName(
            ApplicationUser? user)
        {
            if (user == null)
            {
                return "Unknown User";
            }


            string fullName =
                $"{user.FirstName} {user.LastName}"
                    .Trim();


            if (!string.IsNullOrWhiteSpace(
                fullName))
            {
                return fullName;
            }


            return user.Email ??
                "Unknown User";
        }
    }


    // =============================================================
    // CHIPY USER SUMMARY
    // =============================================================

    public sealed record WorkLogUserSummary(
        TimeSpan TotalCompletedTime,
        IReadOnlyList<WorkLog> ActiveTimers,
        IReadOnlyList<WorkLog> RecentLogs);


    // =============================================================
    // CHIPY PROJECT LEADERBOARD
    // =============================================================

    public sealed record ProjectLeaderboardEntry(
        string UserId,
        string DisplayName,
        TimeSpan TotalTime);


    // =============================================================
    // WEBSITE SUMMARY
    // =============================================================

    public sealed record WorkLogWebsiteSummary(
        TimeSpan MyCompletedTime,
        TimeSpan OverallCompletedTime,
        int ActiveTimerCount,
        DateTimeOffset? MostRecentLogStartedAtUtc);


    // =============================================================
    // EDIT REQUEST
    // =============================================================

    public sealed class WorkLogUpdateRequest
    {
        public int WorkLogId { get; set; }


        public int? ProjectId { get; set; }


        public WorkActivity Activity { get; set; }


        public DateTimeOffset StartedAtUtc { get; set; }


        public DateTimeOffset? EndedAtUtc { get; set; }
    }
}