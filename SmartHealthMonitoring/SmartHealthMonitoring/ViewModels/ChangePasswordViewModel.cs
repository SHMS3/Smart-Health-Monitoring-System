using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ChangePasswordViewModel : IValidatableObject
    {
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        public bool HasPassword { get; set; } = true;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        [DataType(DataType.Password)]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        // Validation thêm: mật khẩu mới không được giống mật khẩu hiện tại
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(NewPassword) &&
                !string.IsNullOrEmpty(CurrentPassword) &&
                NewPassword == CurrentPassword)
            {
                yield return new ValidationResult(
                    "Mật khẩu mới phải khác mật khẩu hiện tại.",
                    new[] { nameof(NewPassword) }
                );
            }
        }
    }
}
