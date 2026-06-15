using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.Models
{
    public class NetworkResource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        [MaxLength(64)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? Description { get; set; }

        [MaxLength(2048)]
        public string? Link { get; set; }

        public int SortOrder { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Course? Course { get; set; }
    }
}
