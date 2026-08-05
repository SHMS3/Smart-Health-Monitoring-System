using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IAdminSettingsService
{
    Task<User?> GetCurrentAdminAsync(int userId);
    Task<bool> IsEmailTakenAsync(int excludeUserId, string email);
    Task UpdateProfileAsync(User admin, string fullName, string email);
    Task ChangePasswordAsync(User admin, string newPassword);
}
