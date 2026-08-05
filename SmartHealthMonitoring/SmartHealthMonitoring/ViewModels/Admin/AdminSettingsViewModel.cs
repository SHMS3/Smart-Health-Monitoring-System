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

    [Required(ErrorMessage = "Vui l�ng nh?p h? v� t�n.")]
    [MaxLength(100, ErrorMessage = "H? v� t�n kh�ng du?c vu?t qu� 100 k� t?.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p email.")]
    [EmailAddress(ErrorMessage = "Email kh�ng h?p l?.")]
    [MaxLength(150, ErrorMessage = "Email kh�ng du?c vu?t qu� 150 k� t?.")]
    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
