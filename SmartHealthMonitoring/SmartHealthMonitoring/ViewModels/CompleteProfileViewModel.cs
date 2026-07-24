using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class CompleteProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
        [SmartHealthMonitoring.Attributes.PastDate(ErrorMessage = "Ngày sinh không được vượt quá ngày hiện tại.")]
        public DateOnly DateOfBirth { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn giới tính.")]
        public byte Sex { get; set; } // 0 = Nữ, 1 = Nam

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng số 0 và có đúng 10 chữ số.")]
        public string Phone { get; set; } = string.Empty;
    }
}
