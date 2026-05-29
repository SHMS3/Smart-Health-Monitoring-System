using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminPatientController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public AdminPatientController(SmartHealthMonitoringContext context) => _context = context;

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
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
                            LockReason = u.LockReason // Lấy từ Migration mới
                        };

            int totalRecords = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var result = new PagedResult<AdminPatientListViewModel>
            {
                Items = items,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            return View(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int userId, string? lockReason)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsDeleted = !user.IsDeleted;
                if (user.IsDeleted)
                {
                    user.LockReason = string.IsNullOrWhiteSpace(lockReason) ? "Không có lý do cụ thể" : lockReason;
                }
                else
                {
                    user.LockReason = null;
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = user.IsDeleted ? "Đã khóa tài khoản bệnh nhân." : "Đã mở khóa tài khoản bệnh nhân.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
