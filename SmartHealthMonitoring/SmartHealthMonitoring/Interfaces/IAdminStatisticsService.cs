using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Interfaces
{
    public interface IAdminStatisticsService
    {
        Task<DashboardStatisticsViewModel> GetDashboardStatisticsAsync();
    }
}
