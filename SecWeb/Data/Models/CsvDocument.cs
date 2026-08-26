using System.ComponentModel.DataAnnotations;

namespace SecWeb.Data.Models
{
    public class CsvDocument
    {
        // Unique ID for this CSV document.
        public int Id { get; set; }

        // ID of the user who owns this CSV file.
        [Required]
        public string UserId { get; set; } = string.Empty;

        // Name displayed to the user on the website.
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Original filename when the user uploaded the file.
        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        // Internal filename used by the website.
        // This prevents two uploaded files with the same name
        // from overwriting each other.
        [Required]
        [MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        // When the CSV file was uploaded.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // When the CSV file was last edited.
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}