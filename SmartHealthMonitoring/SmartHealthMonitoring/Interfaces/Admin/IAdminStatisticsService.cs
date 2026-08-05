using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Interfaces.Admin
{
    public interface IAdminStatisticsService
    {
        Task<DashboardStatisticsViewModel> GetDashboardStatisticsAsync();
        Task<HabitStatisticsViewModel> GetHabitStatisticsAsync();
    }
}
