using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.Models
{
    /// <summary>
    /// Bảng ngưỡng chuẩn — Admin cấu hình 1 lần, bác sĩ áp dụng cho bệnh nhân theo giới tính & độ tuổi.
    /// </summary>
    public class StandardThreshold
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Tên mô tả template, ví dụ: "Nam 41–60 tuổi"</summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Mô tả thêm (tuỳ chọn)</summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>Giới tính áp dụng: 0 = Nữ, 1 = Nam, 2 = Chung (cả hai)</summary>
        public byte Sex { get; set; } = 2;

        /// <summary>Tuổi tối thiểu áp dụng (inclusive)</summary>
        public byte AgeMin { get; set; } = 0;

        /// <summary>Tuổi tối đa áp dụng (inclusive), 120 = không giới hạn trên</summary>
        public byte AgeMax { get; set; } = 120;

        // --- Huyết áp Tâm Thu (Systolic) ---
        public short SystolicBpWarning { get; set; } = 130;
        public short SystolicBpDanger  { get; set; } = 140;

        // --- Huyết áp Tâm Trương (Diastolic) ---
        public short DiastolicBpWarning { get; set; } = 80;
        public short DiastolicBpDanger  { get; set; } = 90;

        // --- Nhịp tim (Heart Rate) ---
        public short HeartRateWarningMin { get; set; } = 60;
        public short HeartRateDangerMin  { get; set; } = 50;
        public short HeartRateWarningMax { get; set; } = 100;
        public short HeartRateDangerMax  { get; set; } = 120;

        /// <summary>Template đang được dùng hay đã bị vô hiệu hóa</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

        public DateTime UpdatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;
    }
}
