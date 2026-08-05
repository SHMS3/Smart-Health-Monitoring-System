using SmartHealthMonitoring.Interfaces.Audit;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services.Admin
{
    public class AdminPatientService : IAdminPatientService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IAuditLogService _auditLogService;

        public AdminPatientService(
            SmartHealthMonitoringContext context,
            IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public async Task<PagedResult<AdminPatientListViewModel>> GetPatientsPagedAsync(int page, int pageSize)
        {
            var query = from u in _context.Users
                        join p in _context.Patients on u.Id equals p.UserId
                        where u.Role == 0
                        orderby u.CreatedAt descending
                        select new AdminPatientListViewModel
                        {
                            UserId = u.Id,
                            PatientId = p.Id,
                            FullName = u.FullName,
                            Email = u.Email,
                            Phone = p.Phone,
                            DateOfBirth = p.DateOfBirth,
                            Sex = p.Sex,
                            IsDeleted = u.IsDeleted,
                            LockReason = u.LockReason
                        };

            int totalRecords = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new PagedResult<AdminPatientListViewModel>
            {
                Items = items,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };
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
                    "PatientAccount",
                    user.Id.ToString(),
                    willLock
                        ? $"Kh�a t�i kho?n b?nh nh�n {user.FullName}. L� do: {user.LockReason}"
                        : $"M? kh�a t�i kho?n b?nh nh�n {user.FullName}.",
                    user.Id,
                    user.FullName);
            }
        }
    }
}

