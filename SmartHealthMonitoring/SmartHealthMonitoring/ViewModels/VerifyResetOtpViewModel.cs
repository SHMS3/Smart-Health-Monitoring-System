using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class VerifyResetOtpViewModel
    {
        [Required]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "M� OTP kh�ng du?c d? tr?ng.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "M� OTP ph?i c� d�ng 6 ch? s?.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "M� OTP ch? bao g?m c�c ch? s?.")]
        [Display(Name = "M� OTP")]
        public string Otp { get; set; } = null!;
    }
}
