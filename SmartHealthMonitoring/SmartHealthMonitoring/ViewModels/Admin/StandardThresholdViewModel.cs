using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels.Admin
{
    public class StandardThresholdViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên template là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tối đa 100 ký tự")]
        [Display(Name = "Tên ngưỡng chuẩn")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Tối đa 500 ký tự")]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giới tính")]
        [Display(Name = "Giới tính áp dụng")]
        public byte Sex { get; set; } = 2;

        [Required]
        [Range(0, 120, ErrorMessage = "Tuổi từ 0–120")]
        [Display(Name = "Tuổi tối thiểu")]
        public byte AgeMin { get; set; } = 0;

        [Required]
        [Range(0, 120, ErrorMessage = "Tuổi từ 0–120")]
        [Display(Name = "Tuổi tối đa")]
        public byte AgeMax { get; set; } = 120;

        [Required][Range(80, 250)][Display(Name = "Cảnh báo HA TT (mmHg)")]
        public short SystolicBpWarning { get; set; } = 130;

        [Required][Range(80, 250)][Display(Name = "Nguy hiểm HA TT (mmHg)")]
        public short SystolicBpDanger { get; set; } = 140;

        [Required][Range(40, 180)][Display(Name = "Cảnh báo HA TR (mmHg)")]
        public short DiastolicBpWarning { get; set; } = 80;

        [Required][Range(40, 180)][Display(Name = "Nguy hiểm HA TR (mmHg)")]
        public short DiastolicBpDanger { get; set; } = 90;

        [Required][Range(20, 100)][Display(Name = "Cảnh báo nhịp tim thấp (bpm)")]
        public short HeartRateWarningMin { get; set; } = 60;

        [Required][Range(20, 100)][Display(Name = "Nguy hiểm nhịp tim thấp (bpm)")]
        public short HeartRateDangerMin { get; set; } = 50;

        [Required][Range(80, 250)][Display(Name = "Cảnh báo nhịp tim cao (bpm)")]
        public short HeartRateWarningMax { get; set; } = 100;

        [Required][Range(80, 250)][Display(Name = "Nguy hiểm nhịp tim cao (bpm)")]
        public short HeartRateDangerMax { get; set; } = 120;

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
