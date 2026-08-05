using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.ViewModels;

public class PatientProfileViewModel
{
    public Patient Patient { get; set; } = null!;
    
    public List<ClinicalRecord> ClinicalRecords { get; set; } = new List<ClinicalRecord>();
    
    public List<DailyVitalLog> DailyVitalLogs { get; set; } = new List<DailyVitalLog>();
    
    public PagedResult<AiriskPrediction> AiPredictions { get; set; } = new PagedResult<AiriskPrediction>();
    
    public List<WarningAlert> WarningAlerts { get; set; } = new List<WarningAlert>();
}
