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

        // --- Fields mới ---
        /// <summary>"Mời tái khám" hoặc "Cảnh báo sức khỏe"</summary>
        public string EmailType { get; set; } = string.Empty;
        /// <summary>Tên bác sĩ gửi, hoặc "Hệ thống tự động" nếu SentByDoctorId = null</summary>
        public string SenderName { get; set; } = "Hệ thống tự động";
        /// <summary>Id của Alert gốc, dùng để hiển thị nút "Xem Alert"</summary>
        public int? AlertId { get; set; }
    }

    public class EmailStats
    {
        public int TotalLast7Days { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int ByAI { get; set; }
        public int ByDoctor { get; set; }
    }

    public class EmailHistoryIndexViewModel
    {
        public List<EmailHistoryDto> Emails { get; set; } = new List<EmailHistoryDto>();
        public byte? FilterStatus { get; set; }
        public string? FilterEmailType { get; set; }
        public string? FilterKeyword { get; set; }
        public int? FilterPatientId { get; set; }
        public string? FilterSender { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int StartItem => TotalItems == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
        public int EndItem => Math.Min(CurrentPage * PageSize, TotalItems);
        public EmailStats Stats { get; set; } = new();

        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "Trạng thái" },
            new SelectListItem { Value = "0", Text = "Chờ gửi" },
            new SelectListItem { Value = "1", Text = "Thành công" },
            new SelectListItem { Value = "2", Text = "Thất bại" },
            new SelectListItem { Value = "3", Text = "Thông báo nội bộ" }
        };

        public List<SelectListItem> EmailTypeOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "Loại email" },
            new SelectListItem { Value = "Mời tái khám", Text = "Mời tái khám" },
            new SelectListItem { Value = "Cảnh báo sức khỏe", Text = "Cảnh báo sức khỏe" },
            new SelectListItem { Value = "Nhắc ghi chỉ số", Text = "Nhắc ghi chỉ số" }
        };

        public List<SelectListItem> PatientOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> SenderOptions { get; set; } = new List<SelectListItem>();
    }
}
