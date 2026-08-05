using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class VerifyRegisterOtpViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui l�ng nh?p m� OTP.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "M� OTP ph?i bao g?m 6 ch? s?.")]
        public string OtpCode { get; set; } = string.Empty;
    }
}
