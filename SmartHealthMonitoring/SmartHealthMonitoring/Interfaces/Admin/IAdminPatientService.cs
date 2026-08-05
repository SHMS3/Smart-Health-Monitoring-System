using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.ViewModels.Admin;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IAdminPatientService
{
    Task<PagedResult<AdminPatientListViewModel>> GetPatientsPagedAsync(int page, int pageSize);
    Task ToggleLockAsync(int userId, string? lockReason);
}
