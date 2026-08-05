using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui l�ng nh?p email.")]
        [EmailAddress(ErrorMessage = "Email kh�ng h?p l?.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui l�ng nh?p m?t kh?u.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
