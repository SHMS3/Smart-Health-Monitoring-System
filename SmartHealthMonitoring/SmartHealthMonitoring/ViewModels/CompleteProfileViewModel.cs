using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class CompleteProfileViewModel
    {
        [Required(ErrorMessage = "Vui l�ng ch?n ng�y sinh.")]
        [SmartHealthMonitoring.Attributes.PastDate(ErrorMessage = "Ng�y sinh kh�ng du?c vu?t qu� ng�y hi?n t?i.")]
        public DateOnly DateOfBirth { get; set; }

        [Required(ErrorMessage = "Vui l�ng ch?n gi?i t�nh.")]
        public byte Sex { get; set; } // 0 = N?, 1 = Nam

        [Required(ErrorMessage = "Vui l�ng nh?p s? di?n tho?i.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "S? di?n tho?i ph?i b?t d?u b?ng s? 0 v� c� d�ng 10 ch? s?.")]
        public string Phone { get; set; } = string.Empty;
    }
}
