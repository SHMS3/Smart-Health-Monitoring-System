using System;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.Models
{
    public class HealthNewsPost
    {
        public int Id { get; set; }

        [Required, MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Summary { get; set; } = string.Empty;

        /// <summary>"PatientStatistics" | "HabitStatistics" | "Manual"</summary>
        [MaxLength(50)]
        public string Source { get; set; } = "Manual";

        /// <summary>"Draft" | "Published" | "Hidden"</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Draft";

        [MaxLength(500)]
        public string? ThumbnailUrl { get; set; }

        [MaxLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
