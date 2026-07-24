using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class VerifyRegisterOtpViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mã OTP.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải bao gồm 6 chữ số.")]
        public string OtpCode { get; set; } = string.Empty;
    }
}
