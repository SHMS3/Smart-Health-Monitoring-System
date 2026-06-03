namespace SmartHealthMonitoring.Services
{
    using SmartHealthMonitoring.Models;
    public interface IWarningAlertService
    {
        Task<List<WarningAlert>> GetAlertsAsync(
       byte? status,
       string? keyword,
       int page,
       int pageSize);

        Task<int> GetTotalAlertsAsync(
            byte? status,
            string? keyword);
        Task<bool> ClaimAlertAsync(int alertId,int doctorId);

        Task<bool> ResolveAlertAsync(int alertId,int doctorId,string resolutionNote);
    }
}
