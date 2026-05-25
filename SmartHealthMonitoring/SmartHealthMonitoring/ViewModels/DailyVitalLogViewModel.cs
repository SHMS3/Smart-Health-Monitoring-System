using System;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class DailyVitalLogViewModel
    {
        public int Id { get; set; }
        public DateTime LoggedAt { get; set; }

        [Range(50, 250, ErrorMessage = "Huyết áp tâm thu không hợp lệ (50-250)")]
        public short SystolicBp { get; set; }   

        [Range(30, 150, ErrorMessage = "Huyết áp tâm trương không hợp lệ (30-150)")]
        public short DiastolicBp { get; set; }
        public string BloodPressureDisplay => $"{SystolicBp}/{DiastolicBp} mmHg";

        [Range(30, 250, ErrorMessage = "Nhịp tim không hợp lệ (30-250)")]
        public short HeartRate { get; set; }

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
        public string AlertLevel
        {
            get
            {
                // 1. CẤP ĐỘ ĐỎ: NGUY HIỂM (Chỉ số vượt ngưỡng quá cao hoặc có triệu chứng lâm sàng nặng)
                if (SystolicBp >= 140 || DiastolicBp >= 90 || HeartRate < 50 || HeartRate > 120 || ChestPainLevel >= 2 || HasExerciseAngina)
                {
                    return "Danger"; // Đỏ
                }

                // 2. CẤP ĐỘ VÀNG: CẦN THEO DÕI (Chỉ số chớm cao hoặc triệu chứng nhẹ)
                if ((SystolicBp >= 130 && SystolicBp < 140) || (DiastolicBp >= 80 && DiastolicBp < 90) || (HeartRate >= 101 && HeartRate <= 120) || (HeartRate >= 50 && HeartRate <= 59) || ChestPainLevel == 1)
                {
                    return "Warning"; // Vàng
                }

                // 3. CẤP ĐỘ XANH: BÌNH THƯỜNG
                return "Normal"; // Xanh
            }
        }

        public string AlertText => AlertLevel switch
        {
            "Danger" => "Nguy hiểm",
            "Warning" => "Cần theo dõi",
            _ => "Bình thường"
        };
    }
}
