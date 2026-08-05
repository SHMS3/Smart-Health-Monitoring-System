using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Doctor
{
    public interface IDoctorService
    {
        Task<SmartHealthMonitoring.Models.Doctor?> GetDoctorByUserIdAsync(int userId);
        Task<List<string>> GetDistinctSpecialtiesAsync();
        Task<List<string?>> GetDistinctRoomNumbersAsync();
        Task<List<SmartHealthMonitoring.Models.Doctor>> GetAllFilteredDoctorsAsync(string? specialty, string? doctorName, byte? gender, string? roomNumber);
    }
}


