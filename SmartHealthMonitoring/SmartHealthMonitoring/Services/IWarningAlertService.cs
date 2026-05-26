namespace SmartHealthMonitoring.Services
{
    using SmartHealthMonitoring.Models;
    public interface IWarningAlertService
    {
        Task<List<WarningAlert>> GetAlertsAsync(byte? status);
        Task<bool> ClaimAlertAsync(int alertId,int doctorId);
    }
}
