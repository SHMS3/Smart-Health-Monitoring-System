using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Interfaces.Patient
{
    public interface IProfileService
    {
        Task<User?> GetUserByIdAsync(int userId);
        Task<SmartHealthMonitoring.Models.Patient?> GetPatientByUserIdAsync(int userId);
        Task<SmartHealthMonitoring.Models.Doctor?> GetDoctorByUserIdAsync(int userId);
        Task UpdateProfileAsync(int userId, UpdateProfileViewModel model);
        Task UpdateHabitsAsync(int userId, HabitViewModel model);
        Task ChangePasswordAsync(int userId, string newPasswordHash);
        Task UpdatePhoneAsync(int userId, string phone);
        Task UpdateAvatarAsync(int userId, string avatarUrl);
        Task<string?> GetAvatarUrlAsync(int userId);
        Task<(int DailyLogCount, int ClinicalRecordCount, int AlertCount, DateTime? LastLogDate)> GetProfileStatsAsync(int userId);
        Task<List<HealthNewsPost>> GetPublishedNewsAsync(int take = 9);
        Task<(List<HealthNewsPost> Items, int TotalCount)> GetNewsPagedAsync(string? keyword, int page, int pageSize);
        Task<(HealthNewsPost? Post, List<HealthNewsPost> Related)> GetNewsDetailAsync(int id);
        Task<int> GetDoctorCountAsync();
        Task<int> GetPatientCountAsync();
        Task<HabitViewModel?> GetHabitsAsync(int userId);
    }
}


