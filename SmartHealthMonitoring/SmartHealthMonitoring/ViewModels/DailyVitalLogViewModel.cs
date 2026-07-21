using System;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class DailyVitalLogViewModel
    {
        public int Id { get; set; }
        public DateTime LoggedAt { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Huyết áp tâm thu")]
        [Range(50, 250, ErrorMessage = "Huyết áp tâm thu không hợp lệ (50-250)")]
        public short? SystolicBp { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập Huyết áp tâm trương")]
        [Range(30, 150, ErrorMessage = "Huyết áp tâm trương không hợp lệ (30-150)")]
        public short? DiastolicBp { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập Nhịp tim")]
        [Range(30, 250, ErrorMessage = "Nhịp tim không hợp lệ (30-250)")]
        public short? HeartRate { get; set; }
        public string BloodPressureDisplay => $"{SystolicBp}/{DiastolicBp} mmHg";

        [Range(0, 10, ErrorMessage = "Mức độ đau phải từ 0 đến 10")]
        public byte ChestPainLevel { get; set; }
        public bool HasExerciseAngina { get; set; }
        public bool IsHighBloodPressure => SystolicBp >= 130 || DiastolicBp >= 80;
        public bool IsAbnormalHeartRate => HeartRate < 60 || HeartRate > 100;

        //cho hàm update log 
        public bool CanUpdate { get; set; }
        public int RemainingUpdate => Math.Max(0,2 - UpdateCount);
        public byte UpdateCount { get; set; }
        public bool IsUpdateLocked { get; set; }

        // Trả về danh sách các lý do vi phạm chỉ số an toàn
        public string AlertLevel { get; set; } = "Normal";

        // Ngưỡng đã được bác sĩ cấu hình (dùng để hiển thị trong Details)
        // Giá trị mặc định khớp với PatientThreshold defaults
        public short SystolicBpWarning { get; set; } = 130;
        public short SystolicBpDanger { get; set; } = 140;
        public short DiastolicBpWarning { get; set; } = 80;
        public short DiastolicBpDanger { get; set; } = 90;
        public short HeartRateWarningMin { get; set; } = 60;
        public short HeartRateDangerMin { get; set; } = 50;
        public short HeartRateWarningMax { get; set; } = 100;
        public short HeartRateDangerMax { get; set; } = 120;

        public string AlertText => AlertLevel switch
        {
            "Danger" => "Nguy hiểm",
            "Warning" => "Cần theo dõi",
            _ => "Bình thường"
        };
    }
}
