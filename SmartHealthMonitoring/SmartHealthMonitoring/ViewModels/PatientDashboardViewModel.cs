namespace SmartHealthMonitoring.ViewModels
{
    public class PatientDashboardViewModel
    {
        // 1. Thông tin bệnh nhân
        public Guid PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? BloodType { get; set; }
        public string? PhoneNumber { get; set; }

        // 2. Lịch sử các chỉ số sức khỏe
        // (Tái sử dụng luôn class HealthMetricHistoryDto mà chúng ta đã tạo ở chức năng trước)
        public List<HealthMetricHistoryDto> MetricsHistory { get; set; } = new List<HealthMetricHistoryDto>();
    }

    public class PatientListDto
    {
        public Guid PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }

}
