namespace SmartHealthMonitoring.Interfaces.AI
{
    using SmartHealthMonitoring.Models;
    using SmartHealthMonitoring.ViewModels;
    using SmartHealthMonitoring.Common;

    public interface IAiWarningAlertService
    {
        Task<List<WarningAlert>> GetAlertsAsync(
           byte? status,
           string? keyword,
           int page,
           int pageSize,
           int? claimedByDoctorId = null);

        Task<int> GetTotalAlertsAsync(
            byte? status,
            string? keyword,
            int? claimedByDoctorId = null);

        Task<ServiceResult> ClaimAlertAsync(
     int alertId,
     int doctorId);
        Task<ServiceResult> ResolveAlertAsync(
     int alertId,
     int doctorId,
     string resolutionNote);
        Task<WarningAlertDetailViewModel?> GetDetailAsync(int id);
        Task<WarningAlert?> GetAlertForResolveAsync(int id);
        Task<WarningAlert?> GetAlertDetailsAsync(int alertId);
        Task<List<ClinicalRecord>> GetClinicalHistoryAsync(int patientId);
        Task<List<DailyVitalLog>> GetDailyHistoryAsync(int patientId);

}
}

