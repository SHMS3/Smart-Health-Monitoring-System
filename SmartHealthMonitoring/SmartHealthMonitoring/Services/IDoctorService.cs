using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public interface IDoctorService
    {
        Task<Doctor?> GetDoctorByUserIdAsync(
            int userId);
    }
}
