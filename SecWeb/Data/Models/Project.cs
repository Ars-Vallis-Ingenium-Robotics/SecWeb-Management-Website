using System.ComponentModel.DataAnnotations;
using SecWeb.Data;

namespace SecWeb.Data.Models
{
    public class Project
    {
        public int Id { get; set; }


        [Required]
        [MaxLength(200)]
        public string Name { get; set; } =
            string.Empty;


        [MaxLength(2000)]
        public string? Description { get; set; }


        public DateTime CreatedAt { get; set; } =
            DateTime.UtcNow;


        // ---------------------------------------------------------
        // PROJECT CREATOR
        // ---------------------------------------------------------

        [Required]
        public string CreatedByUserId { get; set; } =
            string.Empty;

        public ApplicationUser? CreatedByUser { get; set; }


        // ---------------------------------------------------------
        // CLUB-WIDE PROJECT
        // ---------------------------------------------------------

        // AVI Robotics will have this set to true.
        //
        // Everyone may log time to a club-wide project even if
        // they haven't joined a subsystem.
        public bool IsClubWide { get; set; }


        // Protects system projects such as AVI Robotics from
        // being deleted through the website.
        public bool IsProtected { get; set; }


        // ---------------------------------------------------------
        // RELATIONSHIPS
        // ---------------------------------------------------------

        public List<ProjectTeam> Teams { get; set; } =
            new();


        public List<ProjectMembership> Memberships { get; set; } =
            new();


        public List<WorkLog> WorkLogs { get; set; } =
            new();
    }
}