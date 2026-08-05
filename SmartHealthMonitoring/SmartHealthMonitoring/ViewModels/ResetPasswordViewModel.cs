using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "M?t kh?u m?i kh�ng du?c d? tr?ng.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "M?t kh?u ph?i c� �t nh?t {2} k� t?.")]
        [DataType(DataType.Password)]
        [Display(Name = "M?t kh?u m?i")]
        public string NewPassword { get; set; } = null!;

        [DataType(DataType.Password)]
        [Display(Name = "X�c nh?n m?t kh?u m?i")]
        [Compare("NewPassword", ErrorMessage = "M?t kh?u x�c nh?n kh�ng kh?p.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}
