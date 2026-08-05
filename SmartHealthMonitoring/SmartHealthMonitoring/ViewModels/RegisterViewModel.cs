using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui l�ng nh?p email.")]
        [EmailAddress(ErrorMessage = "Email kh�ng h?p l?.")]
        [MaxLength(150, ErrorMessage = "Email kh�ng du?c vu?t qu� 150 k� t?.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui l�ng nh?p m?t kh?u.")]
        [MinLength(6, ErrorMessage = "M?t kh?u ph?i c� �t nh?t 6 k� t?.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui l�ng x�c nh?n m?t kh?u.")]
        [Compare("Password", ErrorMessage = "M?t kh?u x�c nh?n kh�ng kh?p.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
