using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data
{
    public class ApplicationUser : IdentityUser
    {
        // ---------------------------------------------------------
        // PROFILE
        // ---------------------------------------------------------

        [Required]
        [MaxLength(100)]
        [PersonalData]
        public string FirstName { get; set; } =
            string.Empty;


        [Required]
        [MaxLength(100)]
        [PersonalData]
        public string LastName { get; set; } =
            string.Empty;


        // ---------------------------------------------------------
        // DISCORD ACCOUNT LINK
        // ---------------------------------------------------------

        // Discord's permanent user ID.
        //
        // Chipy will use this to find the matching
        // SecWeb account.
        [MaxLength(32)]
        [PersonalData]
        public string? DiscordUserId { get; set; }


        // Example:
        // samjackson
        [MaxLength(100)]
        [PersonalData]
        public string? DiscordUsername { get; set; }


        // Discord server/display name when the link was made.
        [MaxLength(100)]
        [PersonalData]
        public string? DiscordDisplayName { get; set; }


        // Discord avatar identifier.
        [MaxLength(200)]
        [PersonalData]
        public string? DiscordAvatarHash { get; set; }


        // Date the account was connected.
        [PersonalData]
        public DateTimeOffset? DiscordLinkedAtUtc { get; set; }
    }
}