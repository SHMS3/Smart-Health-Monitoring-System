using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Doctor;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces.Doctor
{
    public interface IDoctorScheduleService
    {
        Task<Models.Doctor?> GetDoctorByUserIdAsync(int userId);
        Task<List<AppointmentSlot>> GetWeekSlotsAsync(int doctorId);
        Task CleanupGhostSlotsAsync(int doctorId);
        Task<(bool Success, string? Error)> SaveScheduleAsync(int doctorId, List<DoctorSchedule7DaysDto> slots);
    }
}

