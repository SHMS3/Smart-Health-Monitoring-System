using SmartHealthMonitoring.Common;
using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class PatientListViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public int Age { get; set; }
        public string SexDisplay { get; set; } = null!;
        public string? Phone { get; set; }
    }

    //{
    //}

    public class PatientRecordIndexViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = null!;
        public int Age { get; set; }
        public string SexDisplay { get; set; } = null!;

        public PagedResult<ClinicalRecordSummaryViewModel> Records { get; set; } = new();

        public PagedResult<DailyVitalLogViewModel> DailyLogs { get; set; } = new();

        public DateTime? SearchDate { get; set; }
        public string ActiveTab { get; set; } = "clinical-content";
        public bool HasPaidPaymentToday { get; set; }
        public bool HasClinicalRecordToday { get; set; }
        public bool HasConfiguredThresholds { get; set; }
    }

    public class ClinicalRecordSummaryViewModel
    {
        public int Id { get; set; }
        public DateTime VisitDate { get; set; }
        public int? RestingBP { get; set; }
        public int? Cholesterol { get; set; }
        public int? MaxHeartRate { get; set; }
        public byte? ChestPainType { get; set; }
        public string? ChestPainTypeDisplay { get; set; }
        public byte? FastingBS { get; set; }
        public byte? RestECG { get; set; }
        public byte? ExerciseAngina { get; set; }
        public decimal? OldPeak { get; set; }
        public byte? STSlope { get; set; }
        public byte? MajorVessels { get; set; }
        public byte? ThalResult { get; set; }
        public string? EcgImageUrl { get; set; }
        public string? AttachmentUrl { get; set; }
        public bool IsViewForPatient { get; set; }
    }

    public class ClinicalRecordFormViewModel
    {
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại đau ngực")]
        [Display(Name = "Chest Pain Type")]
        public byte ChestPainType { get; set; }

        [Required(ErrorMessage = "Huyết áp lúc nghỉ là bắt buộc")]
        [Range(0, 300, ErrorMessage = "Giá trị không hợp lệ")]
        [Display(Name = "Resting Blood Pressure (mm Hg)")]
        public short RestingBP { get; set; }

        [Required(ErrorMessage = "Cholesterol là bắt buộc")]
        [Range(0, 1000, ErrorMessage = "Giá trị không hợp lệ")]
        [Display(Name = "Cholesterol (mm/dl)")]
        public short Cholesterol { get; set; }

        [Required]
        [Display(Name = "Fasting Blood Sugar > 120 mg/dl")]
        public byte FastingBS { get; set; }

        [Required]
        [Display(Name = "Resting ECG")]
        public byte RestECG { get; set; }

        [Required]
        [Range(60, 250, ErrorMessage = "Giá trị nhịp tim không hợp lệ")]
        [Display(Name = "Max Heart Rate")]
        public short MaxHeartRate { get; set; }

        [Required]
        [Display(Name = "Exercise Angina")]
        public byte ExerciseAngina { get; set; }

        [Required]
        [Display(Name = "Oldpeak")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "Vui lòng nhập số thập phân hợp lệ")]
        public decimal OldPeak { get; set; }

        [Required]
        [Display(Name = "ST Slope")]
        public byte STSlope { get; set; }

        [Required]
        [Range(0, 3, ErrorMessage = "Giá trị từ 0-3")]
        [Display(Name = "Major Vessels (0-3)")]
        public byte MajorVessels { get; set; }

        [Required]
        [Display(Name = "Thal Rate")]
        public byte ThalResult { get; set; }
    }
}
