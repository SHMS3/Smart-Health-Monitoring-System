using System.ComponentModel.DataAnnotations;

namespace SmartHealthMonitoring.ViewModels
{
    public class ResolveWarningViewModel
    {
        [Required]
        public int WarningAlertId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ghi chú xử lý.")]
        [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string ResolutionNote { get; set; } = string.Empty;

        // Checkbox: Có gửi email mời tái khám kèm theo không?
        public bool SendEmailInvitation { get; set; } = false;

        // Hiển thị thêm thông tin để render trên View
        public string PatientName { get; set; } = string.Empty;
        public string AlertMessage { get; set; } = string.Empty;
    }
}
