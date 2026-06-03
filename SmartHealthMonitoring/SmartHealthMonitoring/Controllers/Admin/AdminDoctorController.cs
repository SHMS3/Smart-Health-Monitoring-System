using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminDoctorController : Controller
    {
        private readonly SmartHealthMonitoringContext _context; 

        public AdminDoctorController(SmartHealthMonitoringContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var query = from u in _context.Users
                        join d in _context.Doctors on u.Id equals d.UserId
                        where u.Role == 1
                        orderby u.CreatedAt descending
                        select new DoctorListViewModel
                        {
                            UserId = u.Id,
                            DoctorId = d.Id,
                            FullName = u.FullName,
                            Email = u.Email,
                            Specialty = d.Specialty,
                            IsOnShift = d.IsOnShift,
                            IsDeleted = u.IsDeleted,
                            LockReason = u.LockReason // Lấy từ Migration mới
                        };

            int totalRecords = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var result = new PagedResult<DoctorListViewModel>
            {
                Items = items,
                TotalCount = totalRecords,
                Page = page,
                PageSize = pageSize
            };

            return View(result);
        }

        [HttpGet]
        public IActionResult Create() => View(new DoctorCreateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DoctorCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"),
                    Role = 1,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    UserId = user.Id,
                    Specialty = model.Specialty,
                    IsOnShift = true,
                    IsDeleted = false
                };
                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                TempData["Success"] = "Đã cấp tài khoản Bác sĩ thành công. Mật khẩu: 123456";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return View(model);
            }
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
                    user.LockReason = null; // Mở khóa thì xóa lý do
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = user.IsDeleted ? "Đã khóa tài khoản bác sĩ." : "Đã mở khóa tài khoản bác sĩ.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
