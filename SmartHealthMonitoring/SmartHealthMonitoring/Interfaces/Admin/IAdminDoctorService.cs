using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.ViewModels.Admin;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IAdminDoctorService
{
    Task<PagedResult<DoctorListViewModel>> GetDoctorsPagedAsync(int page, int pageSize);
    Task<(bool Success, string Message)> CreateDoctorAsync(DoctorCreateViewModel model, string loginUrl);
    Task<DoctorEditViewModel?> GetDoctorForEditAsync(int doctorId);
    Task<(bool Success, string Message)> UpdateDoctorAsync(DoctorEditViewModel model);
    Task ToggleLockAsync(int userId, string? lockReason);
}
