using SmartHealthMonitoring.Interfaces.Audit;
using SmartHealthMonitoring.Interfaces.Email;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services.Admin
{
    public class AdminDoctorService : IAdminDoctorService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;

        public AdminDoctorService(
            SmartHealthMonitoringContext context,
            IAuditLogService auditLogService,
            IEmailService emailService)
        {
            _context = context;
            _auditLogService = auditLogService;
            _emailService = emailService;
        }

        private string GenerateRandomPassword(int length = 8)
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@$?_-";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task<PagedResult<DoctorListViewModel>> GetDoctorsPagedAsync(int page, int pageSize)
        {
            var query = from u in _context.Users
                        join d in _context.Doctors on u.Id equals d.UserId
                        where u.Role == 1
                        orderby u.CreatedAt descending
                        select new global::SmartHealthMonitoring.ViewModels.Admin.DoctorListViewModel
                        {
                            UserId = u.Id,
                            DoctorId = d.Id,
                            FullName = u.FullName,
                            Email = u.Email,
                            Specialty = d.Specialty,
                            IsOnShift = d.IsOnShift,
                            IsDeleted = u.IsDeleted,
                            LockReason = u.LockReason
                        };

            int totalRecords = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<DoctorListViewModel>
            {
                Items = items,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<(bool Success, string Message)> CreateDoctorAsync(DoctorCreateViewModel model, string loginUrl)
        {
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                return (false, "Email n�y d� du?c s? d?ng.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                string randomPassword = GenerateRandomPassword(8);
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var user = new User
                        {
                            FullName = string.IsNullOrWhiteSpace(model.FullName) ? "B�c si chua c?p nh?t" : model.FullName,
                            Email = model.Email,
                            PasswordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword),
                            Role = 1,
                            CreatedAt = DateTime.Now,
                            IsDeleted = false
                        };
                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();

                        var doctor = new global::SmartHealthMonitoring.Models.Doctor
                        {
                            UserId = user.Id,
                            Specialty = model.Specialty,
                            CitizenId = model.CitizenId,
                            PracticeLicense = model.PracticeLicense,
                            DateOfBirth = model.DateOfBirth,
                            Sex = model.Sex,
                            IsOnShift = true,
                            IsDeleted = false
                        };
                        _context.Doctors.Add(doctor);
                        await _context.SaveChangesAsync();

                        await _auditLogService.LogAsync(
                            "Create",
                            "Doctor",
                            doctor.Id.ToString(),
                            $"T?o t�i kho?n b�c si {user.FullName} ({user.Email}).",
                            user.Id,
                            user.FullName);

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                string mailBody = $@"
                    <div style='font-family:Arial,sans-serif;background:#f8f9fa;padding:20px'>
                        <div style='max-width:600px;margin:0 auto;background:#fff;border-radius:12px;padding:30px;box-shadow:0 4px 15px rgba(0,0,0,.1)'>
                            <h2 style='color:#0f172a'>T�i kho?n B�c si du?c t?o th�nh c�ng!</h2>
                            <p style='color:#333;font-size:16px;'>K�nh g?i B�c si <strong>{(string.IsNullOrWhiteSpace(model.FullName) ? "chua c?p nh?t" : model.FullName)}</strong>,</p>
                            <p style='color:#333;font-size:16px;'>H? th?ng SmartHealth d� c?p ph�t t�i kho?n chuy�n gia cho b?n. Du?i d�y l� th�ng tin dang nh?p:</p>
                            <div style='background:#f1f5f9;padding:15px;border-radius:8px;margin:20px 0;'>
                                <p style='margin:0 0 10px;'><strong>Email dang nh?p:</strong> {model.Email}</p>
                                <p style='margin:0;'><strong>M?t kh?u m?c d?nh:</strong> <span style='color:#e11d48;font-weight:bold;font-size:18px;'>{randomPassword}</span></p>
                            </div>
                            <p style='color:#ef4444;font-size:15px;font-weight:bold;'>V� l� do b?o m?t, vui l�ng dang nh?p v� d?i m?t kh?u c?a b?n ngay l?p t?c.</p>
                            <div style='text-align:center;margin:30px 0;'>
                                <a href='{loginUrl}' style='background:#2563eb;color:#fff;padding:12px 24px;text-decoration:none;border-radius:8px;font-weight:bold;display:inline-block;'>�ANG NH?P V� �?I M?T KH?U</a>
                            </div>
                            <hr style='border:none;border-top:1px solid #e2e8f0;margin:30px 0;' />
                            <p style='color:#64748b;font-size:13px;text-align:center;'>��y l� email t? d?ng, vui l�ng kh�ng ph?n h?i.</p>
                        </div>
                    </div>";

                await _emailService.SendEmailAsync(model.Email, "T�i kho?n B�c si - SmartHealth", mailBody);

                return (true, "�� c?p t�i kho?n B�c si th�nh c�ng v� g?i email m?t kh?u m?c d?nh.");
            }
            catch (Exception ex)
            {
                return (false, "L?i h? th?ng: " + ex.Message);
            }
        }

        public async Task<DoctorEditViewModel?> GetDoctorForEditAsync(int doctorId)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == doctorId && !d.IsDeleted);

            if (doctor == null) return null;

            return new global::SmartHealthMonitoring.ViewModels.Admin.DoctorEditViewModel
            {
                DoctorId = doctor.Id,
                UserId = doctor.UserId,
                FullName = doctor.User.FullName,
                Email = doctor.User.Email,
                Specialty = doctor.Specialty,
                CitizenId = doctor.CitizenId,
                PracticeLicense = doctor.PracticeLicense,
                DateOfBirth = doctor.DateOfBirth,
                Sex = doctor.Sex,
                IsOnShift = doctor.IsOnShift
            };
        }

        public async Task<(bool Success, string Message)> UpdateDoctorAsync(DoctorEditViewModel model)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == model.DoctorId && !d.IsDeleted);

            if (doctor == null) return (false, "Kh�ng t�m th?y b�c si.");

            if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != model.UserId))
            {
                return (false, "Email n�y d� du?c s? d?ng b?i ngu?i d�ng kh�c.");
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var oldFullName = doctor.User.FullName;
                        var oldEmail = doctor.User.Email;
                        var oldSpecialty = doctor.Specialty;
                        var oldShiftStatus = doctor.IsOnShift;

                        doctor.User.Email = model.Email;
                        _context.Users.Update(doctor.User);

                        doctor.Specialty = model.Specialty;
                        doctor.PracticeLicense = model.PracticeLicense;
                        doctor.IsOnShift = model.IsOnShift;
                        _context.Doctors.Update(doctor);

                        await _context.SaveChangesAsync();
                        await _auditLogService.LogAsync(
                            "Update",
                            "Doctor",
                            doctor.Id.ToString(),
                            $"C?p nh?t b�c si {oldFullName}; email {oldEmail} -> {model.Email}; chuy�n khoa {oldSpecialty} -> {model.Specialty}; tr?ng th�i tr?c {oldShiftStatus} -> {model.IsOnShift}.",
                            doctor.UserId,
                            oldFullName);

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                return (true, "C?p nh?t th�ng tin b�c si th�nh c�ng.");
            }
            catch (Exception ex)
            {
                return (false, "L?i h? th?ng: " + ex.Message);
            }
        }

        public async Task ToggleLockAsync(int userId, string? lockReason)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                var willLock = !user.IsDeleted;
                user.IsDeleted = !user.IsDeleted;
                if (user.IsDeleted)
                {
                    user.LockReason = string.IsNullOrWhiteSpace(lockReason) ? "Kh�ng c� l� do c? th?" : lockReason;
                }
                else
                {
                    user.LockReason = null;
                }
                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync(
                    willLock ? "Lock" : "Unlock",
                    "DoctorAccount",
                    user.Id.ToString(),
                    willLock
                        ? $"Kh�a t�i kho?n b�c si {user.FullName}. L� do: {user.LockReason}"
                        : $"M? kh�a t�i kho?n b�c si {user.FullName}.",
                    user.Id,
                    user.FullName);
            }
        }
    }
}







