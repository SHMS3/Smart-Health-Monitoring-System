namespace SmartHealthMonitoring.ViewModels
{
    public class PatientDashboardViewModel
    {
        public Guid PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? BloodType { get; set; }
        public string? PhoneNumber { get; set; }

        public List<HealthMetricHistoryDto> MetricsHistory { get; set; } = new List<HealthMetricHistoryDto>();
        public Guid CurrentRecordId { get; set; }
        public List<LabResultDto> LabResults { get; set; } = new List<LabResultDto>();
    }

    public class PatientListDto
    {
        public Guid PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }

    public class LabResultDto
    {
        public Guid LabId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public string? FileUrl { get; set; }
    }

}
