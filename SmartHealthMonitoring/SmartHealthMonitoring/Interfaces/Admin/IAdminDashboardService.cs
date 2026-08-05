using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IAdminDashboardService
{
    Task<AdminDashboardViewModel> GetDashboardSummaryAsync();
}
