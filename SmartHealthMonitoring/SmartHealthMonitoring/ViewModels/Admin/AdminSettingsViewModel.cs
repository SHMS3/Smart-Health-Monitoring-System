using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.ViewModels.Admin;

public class AdminSettingsViewModel
{
    public AdminProfileSettingsViewModel Profile { get; set; } = new();

    public ChangePasswordViewModel Password { get; set; } = new();

    public string ActiveSection { get; set; } = "profile";

    public bool IsGoogleAccount { get; set; }
}

public class AdminProfileSettingsViewModel
{
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [MaxLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class AdminAiAlertSettingsViewModel : IValidatableObject
{
    [Range(1, 5, ErrorMessage = "RiskLevel phải nằm trong khoảng 1 đến 5.")]
    public byte EmergencyRiskLevelThreshold { get; set; } = 3;

    [Range(typeof(decimal), "0.01", "1.00", ErrorMessage = "RiskScore phải nằm trong khoảng 0.01 đến 1.00.")]
    public decimal EmergencyRiskScoreThreshold { get; set; } = 0.70m;

    [Range(0, 120, ErrorMessage = "Tuổi tối thiểu phải nằm trong khoảng 0 đến 120.")]
    public byte EmergencyAgeMin { get; set; } = 0;

    [Range(0, 120, ErrorMessage = "Tuổi tối đa phải nằm trong khoảng 0 đến 120.")]
    public byte EmergencyAgeMax { get; set; } = 120;

    [Range(0, 2, ErrorMessage = "Giới tính áp dụng không hợp lệ.")]
    public byte EmergencySex { get; set; } = 2;

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByAdminName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EmergencyAgeMin > EmergencyAgeMax)
        {
            yield return new ValidationResult(
                "Tuổi tối thiểu không được lớn hơn tuổi tối đa.",
                new[] { nameof(EmergencyAgeMin), nameof(EmergencyAgeMax) });
        }
    }
}
