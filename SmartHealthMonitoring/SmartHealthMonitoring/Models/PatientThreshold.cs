using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.Models
{
    public class PatientThreshold
    {
        [Key]
        public int Id { get; set; }

        public int PatientId { get; set; }

        // --- Cấu hình Huyết áp Tâm Thu (Systolic) ---
        public short SystolicBpWarning { get; set; } = 130; // Từ mức này là Cảnh báo
        public short SystolicBpDanger { get; set; } = 140;  // Từ mức này là Nguy hiểm

        // --- Cấu hình Huyết áp Tâm Trương (Diastolic) ---
        public short DiastolicBpWarning { get; set; } = 80;
        public short DiastolicBpDanger { get; set; } = 90;

        // --- Cấu hình Nhịp tim (Heart Rate) ---
        // Nhịp tim có ngưỡng trên và ngưỡng dưới
        public short HeartRateWarningMin { get; set; } = 60;  // Dưới mức này là Cảnh báo
        public short HeartRateDangerMin { get; set; } = 50;   // Dưới mức này là Nguy hiểm

        public short HeartRateWarningMax { get; set; } = 100; // Trên mức này là Cảnh báo
        public short HeartRateDangerMax { get; set; } = 120;  // Trên mức này là Nguy hiểm

        // Lưu vết cấu hình
        public DateTime UpdatedAt { get; set; }

        public int? UpdatedByDoctorId { get; set; }

        // Navigation Properties
        public virtual Patient Patient { get; set; } = null!;
        public virtual Doctor? UpdatedByDoctor { get; set; }
    }
}
