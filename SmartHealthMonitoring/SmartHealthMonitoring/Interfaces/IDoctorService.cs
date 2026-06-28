using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces
{
    public interface IDoctorService
    {
        Task<Doctor?> GetDoctorByUserIdAsync(
            int userId);
    }
}
