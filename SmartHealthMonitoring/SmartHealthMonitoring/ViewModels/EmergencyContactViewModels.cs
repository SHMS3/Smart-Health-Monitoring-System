using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.ViewModels;

public class EmergencyContactFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên người thân.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2 đến 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mối quan hệ.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Mối quan hệ phải từ 2 đến 50 ký tự.")]
    public string Relationship { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [RegularExpression(@"^[A-Za-z0-9._%+\-]+@[Gg][Mm][Aa][Ii][Ll]\.[Cc][Oo][Mm]$",
        ErrorMessage = "Email nhận SOS phải là địa chỉ Gmail, ví dụ nguoinhan@gmail.com.")]
    [StringLength(150, ErrorMessage = "Email không được vượt quá 150 ký tự.")]
    public string? Email { get; set; }

    [RegularExpression(@"^(0[35789]\d{8}|\+84[35789]\d{8})$",
        ErrorMessage = "Số điện thoại phải là số di động Việt Nam, ví dụ 0901234567 hoặc +84901234567.")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string? Phone { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            yield return new ValidationResult(
                "Vui lòng nhập họ tên người thân.",
                new[] { nameof(FullName) });
        }
        else
        {
            var fullName = FullName.Trim();
            if (!fullName.Any(char.IsLetter))
            {
                yield return new ValidationResult(
                    "Họ tên phải chứa ít nhất một chữ cái.",
                    new[] { nameof(FullName) });
            }

            if (fullName.Any(char.IsDigit) || ContainsUnsafeMarkup(fullName))
            {
                yield return new ValidationResult(
                    "Họ tên không được chứa số hoặc ký tự < >.",
                    new[] { nameof(FullName) });
            }
        }

        if (string.IsNullOrWhiteSpace(Relationship))
        {
            yield return new ValidationResult(
                "Vui lòng nhập mối quan hệ.",
                new[] { nameof(Relationship) });
        }
        else if (!Relationship.Trim().Any(char.IsLetter) || ContainsUnsafeMarkup(Relationship))
        {
            yield return new ValidationResult(
                "Mối quan hệ phải chứa chữ cái và không được chứa ký tự < >.",
                new[] { nameof(Relationship) });
        }

        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Phone))
        {
            yield return new ValidationResult(
                "Vui lòng nhập ít nhất email hoặc số điện thoại.",
                new[] { nameof(Email), nameof(Phone) });
        }

        if (IsPrimary && !IsActive)
        {
            yield return new ValidationResult(
                "Liên hệ chính phải đang bật nhận SOS.",
                new[] { nameof(IsActive) });
        }
    }

    private static bool ContainsUnsafeMarkup(string value)
    {
        return value.Contains('<') || value.Contains('>');
    }
}

public class EmergencyContactIndexViewModel
{
    public List<EmergencyContact> Contacts { get; set; } = new();

    public EmergencyContactFormViewModel Form { get; set; } = new();
}
