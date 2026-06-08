using System;

namespace SmartHealthMonitoring.Models
{
    public class HealthNewsArticle
    {
        public string Title { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime? PubDate { get; set; }
    }
}
