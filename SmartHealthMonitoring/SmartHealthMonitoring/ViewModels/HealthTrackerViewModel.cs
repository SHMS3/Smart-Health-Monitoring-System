using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class HealthTrackerViewModel
    {
        [Required(ErrorMessage = "Vui l�ng ch?n lo?i ch? s?.")]
        public int MetricTypeId { get; set; }

        [Required(ErrorMessage = "Vui l�ng nh?p gi� tr? do.")]
        [Range(0.1, 9999.99, ErrorMessage = "Gi� tr? do kh�ng h?p l?.")]
        public decimal Value { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi ch� kh�ng du?c vu?t qu� 500 k� t?.")]
        public string? Notes { get; set; }

        public List<SelectListItem> AvailableMetrics { get; set; } = new List<SelectListItem>();

        public List<HealthMetricHistoryDto> History { get; set; } = new List<HealthMetricHistoryDto>();

    }

    public class HealthMetricHistoryDto
    {
        public Guid MetricId { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime MeasuredAt { get; set; }
        public string? Notes { get; set; }
    }
}
