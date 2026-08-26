using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data.Models
{
    public class MeetingSegment
    {
        // Unique ID for this meeting segment.
        public int Id { get; set; }

        // ID of the meeting this segment belongs to.
        public int MeetingId { get; set; }

        // Name of this section of the meeting.
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        // Time this segment begins.
        public TimeSpan StartTime { get; set; }

        // Time this segment ends.
        public TimeSpan EndTime { get; set; }

        // Notes written for this segment.
        public string? Notes { get; set; }

        // Determines the order the segments appear in.
        // 0 = first, 1 = second, 2 = third, etc.
        public int SortOrder { get; set; }

        // Connection back to the meeting this segment belongs to.
        public Meeting? Meeting { get; set; }
    }
}