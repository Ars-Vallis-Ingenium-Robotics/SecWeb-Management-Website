using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecWeb.Data.Models;

namespace SecWeb.Data
{
    public class ApplicationDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }


        // =========================================================
        // MEETINGS
        // =========================================================

        public DbSet<Meeting> Meetings =>
            Set<Meeting>();

        public DbSet<MeetingSegment> MeetingSegments =>
            Set<MeetingSegment>();

        public DbSet<MeetingTaskList> MeetingTaskLists =>
            Set<MeetingTaskList>();

        public DbSet<MeetingTask> MeetingTasks =>
            Set<MeetingTask>();

        public DbSet<MeetingTaskFile> MeetingTaskFiles =>
            Set<MeetingTaskFile>();


        // =========================================================
        // CSV
        // =========================================================

        public DbSet<CsvDocument> CsvDocuments =>
            Set<CsvDocument>();


        // =========================================================
        // PROJECTS
        // =========================================================

        public DbSet<Project> Projects =>
            Set<Project>();

        public DbSet<ProjectTeam> ProjectTeams =>
            Set<ProjectTeam>();

        public DbSet<ProjectMembership> ProjectMemberships =>
            Set<ProjectMembership>();


        // =========================================================
        // TIME TRACKING
        // =========================================================

        public DbSet<WorkLog> WorkLogs =>
            Set<WorkLog>();

        public DbSet<WorkLogAudit> WorkLogAudits =>
            Set<WorkLogAudit>();


        // =========================================================
        // DATABASE CONFIGURATION
        // =========================================================

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);


            // =====================================================
            // MEETING -> SEGMENTS
            // =====================================================

            builder.Entity<Meeting>()
                .HasMany(meeting =>
                    meeting.Segments)

                .WithOne(segment =>
                    segment.Meeting)

                .HasForeignKey(segment =>
                    segment.MeetingId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // TEAM -> MEETINGS
            // =====================================================
            //
            // Meetings are attached to a project team/subsystem.
            // If a team is deleted, meeting documentation is kept
            // and becomes unassigned instead of being deleted.
            // =====================================================

            builder.Entity<ProjectTeam>()
                .HasMany(team =>
                    team.Meetings)

                .WithOne(meeting =>
                    meeting.Team)

                .HasForeignKey(meeting =>
                    meeting.ProjectTeamId)

                .OnDelete(DeleteBehavior.SetNull);


            // =====================================================
            // MEETING -> MEMBER TASK TABLES
            // =====================================================

            builder.Entity<Meeting>()
                .HasMany(meeting =>
                    meeting.TaskLists)

                .WithOne(taskList =>
                    taskList.Meeting)

                .HasForeignKey(taskList =>
                    taskList.MeetingId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // TASK TABLE -> ASSIGNED USER
            // =====================================================

            builder.Entity<MeetingTaskList>()
                .HasOne(taskList =>
                    taskList.AssignedUser)

                .WithMany()

                .HasForeignKey(taskList =>
                    taskList.AssignedUserId)

                .OnDelete(DeleteBehavior.NoAction);


            // One task table per member per meeting.

            builder.Entity<MeetingTaskList>()
                .HasIndex(taskList =>
                    new
                    {
                        taskList.MeetingId,
                        taskList.AssignedUserId
                    })

                .IsUnique();


            // =====================================================
            // MEMBER TASK TABLE -> TASKS
            // =====================================================

            builder.Entity<MeetingTaskList>()
                .HasMany(taskList =>
                    taskList.Tasks)

                .WithOne(task =>
                    task.TaskList)

                .HasForeignKey(task =>
                    task.MeetingTaskListId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // TASK -> FILE METADATA
            // =====================================================

            builder.Entity<MeetingTask>()
                .HasMany(task =>
                    task.Files)

                .WithOne(file =>
                    file.MeetingTask)

                .HasForeignKey(file =>
                    file.MeetingTaskId)

                .OnDelete(DeleteBehavior.Cascade);


            // Each task can have one Lead attachment and one
            // member submission file.

            builder.Entity<MeetingTaskFile>()
                .HasIndex(file =>
                    new
                    {
                        file.MeetingTaskId,
                        file.Kind
                    })

                .IsUnique();


            // =====================================================
            // PROJECT -> TEAMS
            // =====================================================

            builder.Entity<Project>()
                .HasMany(project =>
                    project.Teams)

                .WithOne(team =>
                    team.Project)

                .HasForeignKey(team =>
                    team.ProjectId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // PROJECT -> MEMBERSHIPS
            // =====================================================

            builder.Entity<ProjectMembership>()
                .HasOne(membership =>
                    membership.Project)

                .WithMany(project =>
                    project.Memberships)

                .HasForeignKey(membership =>
                    membership.ProjectId)

                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // TEAM -> MEMBERSHIPS
            // =====================================================

            builder.Entity<ProjectMembership>()
                .HasOne(membership =>
                    membership.Team)

                .WithMany(team =>
                    team.Memberships)

                .HasForeignKey(membership =>
                    membership.ProjectTeamId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // USER -> PROJECT MEMBERSHIP
            // =====================================================

            builder.Entity<ProjectMembership>()
                .HasOne(membership =>
                    membership.User)

                .WithMany()

                .HasForeignKey(membership =>
                    membership.UserId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // PROJECT CREATOR
            // =====================================================

            builder.Entity<Project>()
                .HasOne(project =>
                    project.CreatedByUser)

                .WithMany()

                .HasForeignKey(project =>
                    project.CreatedByUserId)

                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // UNIQUE TEAM MEMBERSHIP
            // =====================================================

            builder.Entity<ProjectMembership>()
                .HasIndex(membership =>
                    new
                    {
                        membership.ProjectTeamId,
                        membership.UserId
                    })

                .IsUnique();


            // =====================================================
            // UNIQUE TEAM NAME PER PROJECT
            // =====================================================

            builder.Entity<ProjectTeam>()
                .HasIndex(team =>
                    new
                    {
                        team.ProjectId,
                        team.Name
                    })

                .IsUnique();


            // =====================================================
            // UNIQUE PROJECT NAME
            // =====================================================

            builder.Entity<Project>()
                .HasIndex(project =>
                    project.Name)

                .IsUnique();


            // =====================================================
            // UNIQUE DISCORD ACCOUNT
            // =====================================================

            builder.Entity<ApplicationUser>()
                .HasIndex(user =>
                    user.DiscordUserId)

                .IsUnique()

                .HasFilter(
                    "[DiscordUserId] IS NOT NULL");


            // =====================================================
            // WORK LOG -> USER
            // =====================================================

            builder.Entity<WorkLog>()
                .HasOne(log =>
                    log.User)

                .WithMany()

                .HasForeignKey(log =>
                    log.UserId)

                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // WORK LOG -> PROJECT
            // =====================================================

            builder.Entity<WorkLog>()
                .HasOne(log =>
                    log.Project)

                .WithMany(project =>
                    project.WorkLogs)

                .HasForeignKey(log =>
                    log.ProjectId)

                .OnDelete(DeleteBehavior.SetNull);


            // =====================================================
            // WORK LOG -> LAST EDITOR
            // =====================================================

            builder.Entity<WorkLog>()
                .HasOne(log =>
                    log.LastEditedByUser)

                .WithMany()

                .HasForeignKey(log =>
                    log.LastEditedByUserId)

                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // WORK LOG -> AUDIT HISTORY
            // =====================================================

            builder.Entity<WorkLogAudit>()
                .HasOne(audit =>
                    audit.WorkLog)

                .WithMany(log =>
                    log.AuditHistory)

                .HasForeignKey(audit =>
                    audit.WorkLogId)

                .OnDelete(DeleteBehavior.Cascade);


            // =====================================================
            // AUDIT -> EDITOR
            // =====================================================

            builder.Entity<WorkLogAudit>()
                .HasOne(audit =>
                    audit.EditedByUser)

                .WithMany()

                .HasForeignKey(audit =>
                    audit.EditedByUserId)

                .OnDelete(DeleteBehavior.NoAction);


            // =====================================================
            // WORK LOG INDEXES
            // =====================================================

            builder.Entity<WorkLog>()
                .HasIndex(log =>
                    log.UserId);


            builder.Entity<WorkLog>()
                .HasIndex(log =>
                    log.ProjectId);


            builder.Entity<WorkLog>()
                .HasIndex(log =>
                    log.StartedAtUtc);


            builder.Entity<WorkLog>()
                .HasIndex(log =>
                    log.EndedAtUtc);
        }
    }
}
