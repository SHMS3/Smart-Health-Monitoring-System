using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Auth;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AuthService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public bool ValidatePasswordAsync(User user, string password)
        {
            if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$"))
            {
                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            return password == user.PasswordHash;
        }

        public async Task UpdateDoctorShiftAsync(int userId, bool isOnShift)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (doctor != null)
            {
                doctor.IsOnShift = isOnShift;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<User> FindOrCreateGoogleUserAsync(string email, string fullName)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

            if (user == null)
            {
                var googleStrategy = _context.Database.CreateExecutionStrategy();
                user = await googleStrategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var newUser = new User
                        {
                            FullName = fullName,
                            Email = email,
                            PasswordHash = string.Empty,
                            Role = 0,
                            IsDeleted = false,
                            CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
                        };
                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();

                        var patient = new global::SmartHealthMonitoring.Models.Patient
                        {
                            UserId = newUser.Id,
                            DateOfBirth = new DateOnly(2000, 1, 1),
                            Sex = 0,
                            IsDeleted = false
                        };
                        _context.Patients.Add(patient);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return newUser;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }

            return user;
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user != null && !user.IsDeleted;
        }

        public async Task<bool> ResetPasswordAsync(string email, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
    }
}




