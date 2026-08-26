using System.ComponentModel.DataAnnotations;
using SecWeb.Data;

namespace SecWeb.Data.Models
{
    public class WorkLogAudit
    {
        public int Id { get; set; }


        // ---------------------------------------------------------
        // WORK LOG
        // ---------------------------------------------------------

        public int WorkLogId { get; set; }

        public WorkLog? WorkLog { get; set; }


        // ---------------------------------------------------------
        // WHO MADE THE CHANGE
        // ---------------------------------------------------------

        [Required]
        public string EditedByUserId { get; set; } =
            string.Empty;

        public ApplicationUser? EditedByUser { get; set; }


        public DateTimeOffset EditedAtUtc { get; set; } =
            DateTimeOffset.UtcNow;


        // ---------------------------------------------------------
        // PREVIOUS PROJECT
        // ---------------------------------------------------------

        public int? PreviousProjectId { get; set; }

        [MaxLength(200)]
        public string? PreviousProjectName { get; set; }


        // ---------------------------------------------------------
        // NEW PROJECT
        // ---------------------------------------------------------

        public int? NewProjectId { get; set; }

        [MaxLength(200)]
        public string? NewProjectName { get; set; }


        // ---------------------------------------------------------
        // PREVIOUS / NEW ACTIVITY
        // ---------------------------------------------------------

        public WorkActivity PreviousActivity { get; set; }

        public WorkActivity NewActivity { get; set; }


        // ---------------------------------------------------------
        // PREVIOUS TIMES
        // ---------------------------------------------------------

        public DateTimeOffset PreviousStartedAtUtc { get; set; }

        public DateTimeOffset? PreviousEndedAtUtc { get; set; }


        // ---------------------------------------------------------
        // NEW TIMES
        // ---------------------------------------------------------

        public DateTimeOffset NewStartedAtUtc { get; set; }

        public DateTimeOffset? NewEndedAtUtc { get; set; }
    }
}