using System.ComponentModel.DataAnnotations;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.ViewModels;

public class EmergencyContactFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Vui l�ng nh?p h? t�n ngu?i th�n.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "H? t�n ph?i t? 2 d?n 100 k� t?.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p m?i quan h?.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "M?i quan h? ph?i t? 2 d?n 50 k� t?.")]
    public string Relationship { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email li�n h? kh�ng h?p l?.")]
    [StringLength(150, ErrorMessage = "Email kh�ng du?c vu?t qu� 150 k� t?.")]
    public string? Email { get; set; }

    [RegularExpression(@"^(0[35789]\d{8}|\+84[35789]\d{8})$",
        ErrorMessage = "S? di?n tho?i ph?i l� s? di d?ng Vi?t Nam, v� d? 0901234567 ho?c +84901234567.")]
    [StringLength(20, ErrorMessage = "S? di?n tho?i kh�ng du?c vu?t qu� 20 k� t?.")]
    public string? Phone { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            yield return new ValidationResult(
                "Vui l�ng nh?p h? t�n ngu?i th�n.",
                new[] { nameof(FullName) });
        }
        else
        {
            var fullName = FullName.Trim();
            if (!fullName.Any(char.IsLetter))
            {
                yield return new ValidationResult(
                    "H? t�n ph?i ch?a �t nh?t m?t ch? c�i.",
                    new[] { nameof(FullName) });
            }

            if (fullName.Any(char.IsDigit) || ContainsUnsafeMarkup(fullName))
            {
                yield return new ValidationResult(
                    "H? t�n kh�ng du?c ch?a s? ho?c k� t? < >.",
                    new[] { nameof(FullName) });
            }
        }

        if (string.IsNullOrWhiteSpace(Relationship))
        {
            yield return new ValidationResult(
                "Vui l�ng nh?p m?i quan h?.",
                new[] { nameof(Relationship) });
        }
        else if (!Relationship.Trim().Any(char.IsLetter) || ContainsUnsafeMarkup(Relationship))
        {
            yield return new ValidationResult(
                "M?i quan h? ph?i ch?a ch? c�i v� kh�ng du?c ch?a k� t? < >.",
                new[] { nameof(Relationship) });
        }

        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Phone))
        {
            yield return new ValidationResult(
                "Vui l�ng nh?p �t nh?t email ho?c s? di?n tho?i.",
                new[] { nameof(Email), nameof(Phone) });
        }

        if (IsPrimary && !IsActive)
        {
            yield return new ValidationResult(
                "Li�n h? ch�nh ph?i dang ? tr?ng th�i s? d?ng.",
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
