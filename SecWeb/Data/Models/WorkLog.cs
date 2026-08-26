using System.ComponentModel.DataAnnotations;
using SecWeb.Data;

namespace SecWeb.Data.Models
{
    public class WorkLog
    {
        // ---------------------------------------------------------
        // PRIMARY KEY
        // ---------------------------------------------------------

        public int Id { get; set; }


        // ---------------------------------------------------------
        // USER
        // ---------------------------------------------------------

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }


        // ---------------------------------------------------------
        // PROJECT
        // ---------------------------------------------------------

        // Nullable so historical time survives if the project
        // is eventually deleted.
        public int? ProjectId { get; set; }

        public Project? Project { get; set; }


        // Keeps the project name that existed when the time
        // was originally logged.
        //
        // Example:
        //
        // MATE ROV 2027
        //
        // If that project is deleted later, the historical
        // time record can still display its original project.
        [Required]
        [MaxLength(200)]
        public string ProjectNameSnapshot { get; set; } =
            string.Empty;


        // ---------------------------------------------------------
        // ACTIVITY
        // ---------------------------------------------------------

        public WorkActivity Activity { get; set; }


        // ---------------------------------------------------------
        // TIME
        // ---------------------------------------------------------

        // All times are stored in UTC.
        public DateTimeOffset StartedAtUtc { get; set; }

        // null means that the timer is currently running.
        public DateTimeOffset? EndedAtUtc { get; set; }


        // ---------------------------------------------------------
        // SOURCE
        // ---------------------------------------------------------

        public WorkLogSource Source { get; set; }


        // ---------------------------------------------------------
        // DISCORD INFORMATION
        // ---------------------------------------------------------

        // These are populated when the log originated from Chipy.

        [MaxLength(32)]
        public string? DiscordGuildId { get; set; }

        [MaxLength(32)]
        public string? DiscordChannelId { get; set; }


        // ---------------------------------------------------------
        // RECORD INFORMATION
        // ---------------------------------------------------------

        public DateTimeOffset CreatedAtUtc { get; set; } =
            DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAtUtc { get; set; } =
            DateTimeOffset.UtcNow;


        // ---------------------------------------------------------
        // LAST EDIT
        // ---------------------------------------------------------

        public string? LastEditedByUserId { get; set; }

        public ApplicationUser? LastEditedByUser { get; set; }

        public DateTimeOffset? LastEditedAtUtc { get; set; }


        // ---------------------------------------------------------
        // AUDIT HISTORY
        // ---------------------------------------------------------

        public List<WorkLogAudit> AuditHistory { get; set; } =
            new();


        // ---------------------------------------------------------
        // CALCULATED HELPERS
        // ---------------------------------------------------------

        public bool IsActive =>
            EndedAtUtc == null;


        public TimeSpan GetDuration(
            DateTimeOffset? currentTime = null)
        {
            DateTimeOffset end =
                EndedAtUtc ??
                currentTime ??
                DateTimeOffset.UtcNow;

            return end - StartedAtUtc;
        }
    }
}