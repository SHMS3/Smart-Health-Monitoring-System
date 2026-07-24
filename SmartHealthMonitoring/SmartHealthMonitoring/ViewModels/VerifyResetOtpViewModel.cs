using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class VerifyResetOtpViewModel
    {
        [Required]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mã OTP không được để trống.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có đúng 6 chữ số.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ bao gồm các chữ số.")]
        [Display(Name = "Mã OTP")]
        public string Otp { get; set; } = null!;
    }
}
