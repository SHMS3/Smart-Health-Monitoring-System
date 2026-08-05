using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ProfileViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsGoogleAccount { get; set; } // PasswordHash rỗng = đăng nhập Google
        public string? AvatarUrl { get; set; }

        public int? PatientId { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public byte? Sex { get; set; }      // 0 = Nữ, 1 = Nam
        public string? Phone { get; set; }
        public bool IsPhoneVerified { get; set; }
        public string? Address { get; set; }

        public string? CitizenId { get; set; }
        public string? CitizenIdFrontUrl { get; set; }
        public string? CitizenIdBackUrl { get; set; }
        public string? PracticeLicense { get; set; }
        public string? Specialty { get; set; }

        public int TotalVitalLogs { get; set; }
        public int TotalClinicalRecords { get; set; }
        public int TotalWarningAlerts { get; set; }
        public DateTime? LastLogAt { get; set; }

        public int? Age => DateOfBirth.HasValue
            ? (int)((DateTime.Today - DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365.25)
            : null;

        public string GenderDisplay => Sex switch
        {
            1 => "Nam",
            0 => "Nữ",
            _ => "Chưa cập nhật"
        };

        public byte Role { get; set; }

        public string RoleDisplay => Role switch
        {
            0 => "Bệnh nhân",
            1 => "Bác sĩ",
            2 => "Quản trị viên",
            _ => "Người dùng"
        };

        public HabitViewModel? Habit { get; set; }
    }

    public class HabitViewModel
    {
        public bool DietSalty { get; set; }
        public bool DietHighFat { get; set; }
        public bool DietHighSugar { get; set; }
        public bool DietLowFiber { get; set; }
        public bool AlcoholHeavy { get; set; }
        public bool CaffeineSpike { get; set; }

        public bool LifestyleSedentary { get; set; }
        public bool LifestyleSitLong { get; set; }
        public bool SleepDeprived { get; set; }
        public bool NoHealthCheck { get; set; }

        public bool SmokeActive { get; set; }
        public bool SmokePassive { get; set; }
        public bool SelfMedication { get; set; }

        public bool StressHigh { get; set; }

        public bool ExerciseRegularly { get; set; }
        public bool SleepEarly { get; set; }
        public bool DrinkEnoughWater { get; set; }
        public bool DietBalanced { get; set; }
        public bool RegularHealthCheck { get; set; }
        public bool NoSubstanceAbuse { get; set; }
    }

    public class UpdateProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [SmartHealthMonitoring.Attributes.PastDate(ErrorMessage = "Ngày sinh không được vượt quá ngày hiện tại.")]
        public DateOnly? DateOfBirth { get; set; }

        public byte? Sex { get; set; }

        [RegularExpression(@"^(0|\+84)[0-9]{9}$", ErrorMessage = "Số điện thoại không hợp lệ (VD: 0912345678 hoặc +84912345678).")]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [RegularExpression(@"^\d{12}$", ErrorMessage = "Căn cước công dân phải bao gồm đúng 12 chữ số.")]
        public string? CitizenId { get; set; }

        [MaxLength(100, ErrorMessage = "Giấy phép hành nghề không được vượt quá 100 ký tự.")]
        public string? PracticeLicense { get; set; }

        public string? AvatarUrl { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? AvatarFile { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CitizenIdFrontFile { get; set; }
        public Microsoft.AspNetCore.Http.IFormFile? CitizenIdBackFile { get; set; }
    }
}
