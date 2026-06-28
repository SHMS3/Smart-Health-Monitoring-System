using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels.Admin
{
    // 1. Dashboard ViewModels
    public class AdminDashboardViewModel
    {
        public int TotalDoctors { get; set; }
        public int TotalPatients { get; set; }
        public int TotalClinicalRecords { get; set; }
        public int TotalPendingAlerts { get; set; }
        public List<RecentAlertViewModel> RecentAlerts { get; set; } = new();
    }

    public class RecentAlertViewModel
    {
        public int AlertId { get; set; }
        public string PatientName { get; set; } = null!;
        public string WarningLevel { get; set; } = null!;
        public DateTime FlaggedAt { get; set; } // Chuẩn theo DB
    }

    // 2. Quản lý Bác sĩ ViewModels
    public class DoctorListViewModel
    {
        public int UserId { get; set; }
        public int DoctorId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Specialty { get; set; } = null!;
        public bool IsOnShift { get; set; }
        public bool IsDeleted { get; set; } 
        public string? LockReason { get; set; }
    }

    public class DoctorCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập Chuyên khoa")]
        public string Specialty { get; set; } = null!;

        [RegularExpression(@"^\d{12}$", ErrorMessage = "Căn cước công dân phải bao gồm đúng 12 chữ số.")]
        public string? CitizenId { get; set; }

        [MaxLength(100, ErrorMessage = "Giấy phép hành nghề không được vượt quá 100 ký tự.")]
        public string? PracticeLicense { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public byte? Sex { get; set; }
    }

    public class DoctorEditViewModel
    {
        public int UserId { get; set; }
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập Chuyên khoa")]
        public string Specialty { get; set; } = null!;

        [RegularExpression(@"^\d{12}$", ErrorMessage = "Căn cước công dân phải bao gồm đúng 12 chữ số.")]
        public string? CitizenId { get; set; }

        [MaxLength(100, ErrorMessage = "Giấy phép hành nghề không được vượt quá 100 ký tự.")]
        public string? PracticeLicense { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public byte? Sex { get; set; }

        public bool IsOnShift { get; set; }
    }

    // 3. Quản lý Bệnh nhân ViewModels
    public class AdminPatientListViewModel
    {
        public int UserId { get; set; }
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; } // Chuẩn theo varchar(15) NULL của DB
        public DateOnly DateOfBirth { get; set; }
        public byte Sex { get; set; }
        public bool IsDeleted { get; set; }
        public string? LockReason { get; set; }
    }

    // 4. Statistics ViewModels
    public class PatientDemographicStatsViewModel
    {
        public List<string> AgeLabels { get; set; } = new();
        public List<int> AgeValues { get; set; } = new();
        public List<string> SexLabels { get; set; } = new();
        public List<int> SexValues { get; set; } = new();
    }

    public class ClinicalSymptomsStatsViewModel
    {
        public List<string> ChestPainLabels { get; set; } = new();
        public List<int> ChestPainValues { get; set; } = new();
        public double AverageCholesterolAge40To50 { get; set; }
        public double FastingBsHighRate { get; set; }
    }

    public class DashboardStatisticsViewModel
    {
        public PatientDemographicStatsViewModel Demographics { get; set; } = new();
        public ClinicalSymptomsStatsViewModel Symptoms { get; set; } = new();
        public HabitStatisticsViewModel Habits { get; set; } = new();
    }

    // 5. Habit Statistics ViewModels
    public class HabitItemViewModel
    {
        public string Key { get; set; } = null!;
        public string Label { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Category { get; set; } = null!;   // "Ăn uống" | "Sinh hoạt" | "Hành vi" | "Tâm lý"
        public string Type { get; set; } = null!;        // "bad" | "good"
        public string Icon { get; set; } = null!;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class HabitCategoryViewModel
    {
        public string Name { get; set; } = null!;
        public string Icon { get; set; } = null!;
        public string ColorClass { get; set; } = null!;
        public List<HabitItemViewModel> Items { get; set; } = new();
    }

    public class HabitStatisticsViewModel
    {
        public int TotalPatientsWithHabit { get; set; }
        public int TotalPatients { get; set; }
        public List<HabitCategoryViewModel> Categories { get; set; } = new();
        // Top 5 thói quen xấu phổ biến nhất (for bar chart)
        public List<string> TopBadHabitLabels { get; set; } = new();
        public List<int> TopBadHabitValues { get; set; } = new();
        // Top 5 thói quen tốt phổ biến nhất (for bar chart)
        public List<string> TopGoodHabitLabels { get; set; } = new();
        public List<int> TopGoodHabitValues { get; set; } = new();
        // Phân bố số lượng thói quen xấu mỗi bệnh nhân
        public List<string> BadHabitDistributionLabels { get; set; } = new();
        public List<int> BadHabitDistributionValues { get; set; } = new();
    }
}
