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
