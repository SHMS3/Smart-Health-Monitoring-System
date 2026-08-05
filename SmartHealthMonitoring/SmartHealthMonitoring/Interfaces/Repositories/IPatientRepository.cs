using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Repositories;

public interface IPatientRepository
{
    Task<SmartHealthMonitoring.Models.Patient?> GetByUserIdAsync(int userId);
}
