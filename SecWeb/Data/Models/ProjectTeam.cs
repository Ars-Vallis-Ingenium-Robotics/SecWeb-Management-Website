using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data.Models
{
    public class ProjectTeam
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public Project? Project { get; set; }

        public List<ProjectMembership> Memberships { get; set; } = new();

        public List<Meeting> Meetings { get; set; } = new();
    }
}
