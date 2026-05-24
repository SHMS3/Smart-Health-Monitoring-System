using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class HealthTrackerViewModel
    {
        // ==========================================
        // 1. DỮ LIỆU ĐỂ BINDING FORM NHẬP (POST)
        // ==========================================
        [Required(ErrorMessage = "Vui lòng chọn loại chỉ số.")]
        public int MetricTypeId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập giá trị đo.")]
        [Range(0.1, 9999.99, ErrorMessage = "Giá trị đo không hợp lệ.")]
        public decimal Value { get; set; }

        [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Notes { get; set; }

        // Danh sách để load vào Dropdown Select trên giao diện (Huyết áp, Chiều cao, Cân nặng...)
        public List<SelectListItem> AvailableMetrics { get; set; } = new List<SelectListItem>();

        // ==========================================
        // 2. DỮ LIỆU ĐỂ HIỂN THỊ LÊN BẢNG LỊCH SỬ (GET)
        // ==========================================
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
