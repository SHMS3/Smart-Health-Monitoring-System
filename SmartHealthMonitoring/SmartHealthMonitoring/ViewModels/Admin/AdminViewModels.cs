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
}
