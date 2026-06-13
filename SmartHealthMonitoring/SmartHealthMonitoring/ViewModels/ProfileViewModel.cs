using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ProfileViewModel
    {
        // ── Thông tin tài khoản ──
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsGoogleAccount { get; set; } // PasswordHash rỗng = đăng nhập Google

        // ── Thông tin bệnh nhân (chỉ dành cho Role = 0) ──
        public int? PatientId { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public byte? Sex { get; set; }      // 0 = Nữ, 1 = Nam
        public string? Phone { get; set; }
        public bool IsPhoneVerified { get; set; }
        public string? Address { get; set; }

        // ── Căn cước & Giấy phép ──
        public string? CitizenId { get; set; }
        public string? PracticeLicense { get; set; }
        public string? Specialty { get; set; }

        // ── Thống kê nhanh ──
        public int TotalVitalLogs { get; set; }
        public int TotalClinicalRecords { get; set; }
        public int TotalWarningAlerts { get; set; }
        public DateTime? LastLogAt { get; set; }

        // ── Computed properties ──
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
    }

    // ViewModel riêng để cập nhật thông tin cá nhân
    public class UpdateProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

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
    }
}
