using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data.Models
{
    public class MeetingTaskFile
    {
        public int Id { get; set; }

        public int MeetingTaskId { get; set; }

        public MeetingTaskFileKind Kind { get; set; }

        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(260)]
        public string StoredFileName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        public MeetingTask? MeetingTask { get; set; }
    }
}
