using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class PatientThresholdViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string SexDisplay { get; set; } = string.Empty;
        public byte Sex { get; set; } = 1;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(80, 250, ErrorMessage = "Giá trị từ 80–250 mmHg")]
        [Display(Name = "Cảnh báo Huyết áp TT (mmHg)")]
        public short SystolicBpWarning { get; set; } = 130;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(80, 250, ErrorMessage = "Giá trị từ 80–250 mmHg")]
        [Display(Name = "Nguy hiểm Huyết áp TT (mmHg)")]
        public short SystolicBpDanger { get; set; } = 140;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(40, 180, ErrorMessage = "Giá trị từ 40–180 mmHg")]
        [Display(Name = "Cảnh báo Huyết áp TT (mmHg)")]
        public short DiastolicBpWarning { get; set; } = 80;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(40, 180, ErrorMessage = "Giá trị từ 40–180 mmHg")]
        [Display(Name = "Nguy hiểm Huyết áp TT (mmHg)")]
        public short DiastolicBpDanger { get; set; } = 90;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(20, 100, ErrorMessage = "Giá trị từ 20–100 bpm")]
        [Display(Name = "Cảnh báo Nhịp tim thấp (bpm)")]
        public short HeartRateWarningMin { get; set; } = 60;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(20, 100, ErrorMessage = "Giá trị từ 20–100 bpm")]
        [Display(Name = "Nguy hiểm Nhịp tim thấp (bpm)")]
        public short HeartRateDangerMin { get; set; } = 50;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(80, 250, ErrorMessage = "Giá trị từ 80–250 bpm")]
        [Display(Name = "Cảnh báo Nhịp tim cao (bpm)")]
        public short HeartRateWarningMax { get; set; } = 100;

        [Required(ErrorMessage = "Bắt buộc nhập")]
        [Range(80, 250, ErrorMessage = "Giá trị từ 80–250 bpm")]
        [Display(Name = "Nguy hiểm Nhịp tim cao (bpm)")]
        public short HeartRateDangerMax { get; set; } = 120;

        public bool IsConfigured { get; set; } = false;
        public int? ThresholdId { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public int? LastUpdatedByDoctorId { get; set; }
        public string? LastUpdatedByDoctor { get; set; }
        public string? LastUpdatedByDoctorSpecialty { get; set; }
    }
}
