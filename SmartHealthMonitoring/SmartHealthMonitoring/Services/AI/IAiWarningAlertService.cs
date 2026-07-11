namespace SmartHealthMonitoring.Services.AI
{
    using SmartHealthMonitoring.Models;
    using SmartHealthMonitoring.ViewModels;

    /// <summary>
    /// Interface cho service quản lý cảnh báo nguy cơ tim mạch (WarningAlert).
    /// Bao gồm: phân trang + tìm kiếm, claim, resolve.
    /// </summary>
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
    }

}
