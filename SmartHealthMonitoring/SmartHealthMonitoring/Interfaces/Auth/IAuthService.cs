using System.Threading.Tasks;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<User?> FindByEmailAsync(string email);
        bool ValidatePasswordAsync(User user, string password);
        Task UpdateDoctorShiftAsync(int userId, bool isOnShift);
        Task<User> FindOrCreateGoogleUserAsync(string email, string fullName);
        Task<bool> UserExistsAsync(string email);
        Task<bool> ResetPasswordAsync(string email, string newPassword);
    }
}
