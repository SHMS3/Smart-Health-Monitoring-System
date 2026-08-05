using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email kh�ng du?c d? tr?ng.")]
        [EmailAddress(ErrorMessage = "Email kh�ng h?p l?.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = null!;
    }
}
