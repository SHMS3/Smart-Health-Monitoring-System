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
           int pageSize);

        Task<int> GetTotalAlertsAsync(
            byte? status,
            string? keyword);

        Task<bool> ClaimAlertAsync(int alertId, int doctorId);

        Task<bool> ResolveAlertAsync(int alertId, int doctorId, string resolutionNote);
        Task<WarningAlertDetailViewModel?> GetDetailAsync(int id);
    }
}
