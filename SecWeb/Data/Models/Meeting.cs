using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data.Models
{
    public class Meeting
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int? ProjectTeamId { get; set; }

        public ProjectTeam? Team { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public DateTime MeetingDate { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<MeetingSegment> Segments { get; set; } = new();

        public List<MeetingTaskList> TaskLists { get; set; } = new();
    }
}
