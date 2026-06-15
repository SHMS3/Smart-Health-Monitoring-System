using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.ViewModels
{
    public class WarningAlertDetailViewModel
    {
        public WarningAlert Alert { get; set; } = null!;

        public List<DailyVitalLog> RecentVitalLogs { get; set; } = new();

        public List<ClinicalRecord> ClinicalRecords { get; set; } = new();
    }
}
