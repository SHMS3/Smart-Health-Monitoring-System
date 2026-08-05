using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.Admin
{
    public class AdminSettingsService : IAdminSettingsService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AdminSettingsService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<User?> GetCurrentAdminAsync(int userId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.Role == 2 && !u.IsDeleted);
        }

        public async Task<bool> IsEmailTakenAsync(int excludeUserId, string email)
        {
            return await _context.Users.AnyAsync(u => u.Id != excludeUserId && u.Email == email);
        }

        public async Task UpdateProfileAsync(User admin, string fullName, string email)
        {
            admin.FullName = fullName;
            admin.Email = email;
            await _context.SaveChangesAsync();
        }

        public async Task ChangePasswordAsync(User admin, string newPassword)
        {
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
        }
    }
}
