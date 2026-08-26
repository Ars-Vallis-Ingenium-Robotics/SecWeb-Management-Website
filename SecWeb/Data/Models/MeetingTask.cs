using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data.Models
{
    public class MeetingTask
    {
        public int Id { get; set; }

        public int MeetingTaskListId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(5000)]
        public string? Details { get; set; }

        public int SortOrder { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [MaxLength(5000)]
        public string? SubmissionComments { get; set; }

        public DateTimeOffset? SubmittedAtUtc { get; set; }

        public MeetingTaskList? TaskList { get; set; }

        public List<MeetingTaskFile> Files { get; set; } = new();
    }
}
