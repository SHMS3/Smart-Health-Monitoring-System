using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartHealthMonitoring.ViewModels
{
    public class EmailHistoryDto
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string Body { get; set; } = string.Empty;
    }

    public class EmailHistoryIndexViewModel
    {
        public List<EmailHistoryDto> Emails { get; set; } = new List<EmailHistoryDto>();
        public byte? FilterStatus { get; set; }
        
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "Tất cả" },
            new SelectListItem { Value = "0", Text = "Chờ gửi" },
            new SelectListItem { Value = "1", Text = "Thành công" },
            new SelectListItem { Value = "2", Text = "Thất bại" }
        };
    }
}
