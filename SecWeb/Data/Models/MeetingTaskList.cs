using System.ComponentModel.DataAnnotations;
using SecWeb.Data;

namespace SecWeb.Data.Models
{
    public class MeetingTaskList
    {
        public int Id { get; set; }

        public int MeetingId { get; set; }

        [Required]
        public string AssignedUserId { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public Meeting? Meeting { get; set; }

        public ApplicationUser? AssignedUser { get; set; }

        public List<MeetingTask> Tasks { get; set; } = new();
    }
}
