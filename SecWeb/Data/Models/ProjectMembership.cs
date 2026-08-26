using System.ComponentModel.DataAnnotations;
using SecWeb.Data;

namespace SecWeb.Data.Models
{
    public class ProjectMembership
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public int ProjectTeamId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ProjectRole Role { get; set; } =
            ProjectRole.Member;

        public DateTime JoinedAt { get; set; } =
            DateTime.UtcNow;

        // Navigation properties

        public Project? Project { get; set; }

        public ProjectTeam? Team { get; set; }

        public ApplicationUser? User { get; set; }
    }
}