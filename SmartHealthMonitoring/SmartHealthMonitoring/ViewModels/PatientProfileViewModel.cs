using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.ViewModels;

public class PatientProfileViewModel
{
    public Patient Patient { get; set; } = null!;
    
    // Tất cả hồ sơ lâm sàng (ClinicalRecords)
    public List<ClinicalRecord> ClinicalRecords { get; set; } = new List<ClinicalRecord>();
    
    // 30 Logs gần nhất để vẽ biểu đồ
    public List<DailyVitalLog> DailyVitalLogs { get; set; } = new List<DailyVitalLog>();
    
    // Lịch sử điểm rủi ro
    public PagedResult<AiriskPrediction> AiPredictions { get; set; } = new PagedResult<AiriskPrediction>();
    
    // Các cảnh báo liên quan
    public List<WarningAlert> WarningAlerts { get; set; } = new List<WarningAlert>();
}
