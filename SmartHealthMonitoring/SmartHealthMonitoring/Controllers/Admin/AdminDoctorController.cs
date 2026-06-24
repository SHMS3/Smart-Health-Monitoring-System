using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminDoctorController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IAuditLogService _auditLogService;
        private readonly IEmailService _emailService;

        public AdminDoctorController(
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
                string randomPassword = GenerateRandomPassword(8);
                var user = new User
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword),
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
                    $"Tạo tài khoản bác sĩ {user.FullName} ({user.Email}).",
                    user.Id,
                    user.FullName);

                await transaction.CommitAsync();

                // Send email to the doctor
                string loginUrl = Url.Action("Login", "Auth", new { returnUrl = "/Home/Profile?tab=security" }, Request.Scheme) ?? "";
                string mailBody = $@"
                    <div style='font-family:Arial,sans-serif;background:#f8f9fa;padding:20px'>
                        <div style='max-width:600px;margin:0 auto;background:#fff;border-radius:12px;padding:30px;box-shadow:0 4px 15px rgba(0,0,0,.1)'>
                            <h2 style='color:#0f172a'>Tài khoản Bác sĩ được tạo thành công!</h2>
                            <p style='color:#333;font-size:16px;'>Kính gửi Bác sĩ <strong>{model.FullName}</strong>,</p>
                            <p style='color:#333;font-size:16px;'>Hệ thống SmartHealth đã cấp phát tài khoản chuyên gia cho bạn. Dưới đây là thông tin đăng nhập:</p>
                            <div style='background:#f1f5f9;padding:15px;border-radius:8px;margin:20px 0;'>
                                <p style='margin:0 0 10px;'><strong>Email đăng nhập:</strong> {model.Email}</p>
                                <p style='margin:0;'><strong>Mật khẩu mặc định:</strong> <span style='color:#e11d48;font-weight:bold;font-size:18px;'>{randomPassword}</span></p>
                            </div>
                            <p style='color:#ef4444;font-size:15px;font-weight:bold;'>Vì lý do bảo mật, vui lòng đăng nhập và đổi mật khẩu của bạn ngay lập tức.</p>
                            <div style='text-align:center;margin:30px 0;'>
                                <a href='{loginUrl}' style='background:#2563eb;color:#fff;padding:12px 24px;text-decoration:none;border-radius:8px;font-weight:bold;display:inline-block;'>ĐĂNG NHẬP VÀ ĐỔI MẬT KHẨU</a>
                            </div>
                            <hr style='border:none;border-top:1px solid #e2e8f0;margin:30px 0;' />
                            <p style='color:#64748b;font-size:13px;text-align:center;'>Đây là email tự động, vui lòng không phản hồi.</p>
                        </div>
                    </div>";

                await _emailService.SendEmailAsync(model.Email, "Tài khoản Bác sĩ - SmartHealth", mailBody);

                TempData["Success"] = "Đã cấp tài khoản Bác sĩ thành công và gửi email mật khẩu mặc định.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Lỗi hệ thống: " + ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (doctor == null) return NotFound();

            var model = new DoctorEditViewModel
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

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DoctorEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == model.DoctorId && !d.IsDeleted);

            if (doctor == null) return NotFound();

            if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.Id != model.UserId))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng bởi người dùng khác.");
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldFullName = doctor.User.FullName;
                var oldEmail = doctor.User.Email;
                var oldSpecialty = doctor.Specialty;
                var oldShiftStatus = doctor.IsOnShift;

                doctor.User.FullName = model.FullName;
                doctor.User.Email = model.Email;
                _context.Users.Update(doctor.User);

                doctor.Specialty = model.Specialty;
                doctor.CitizenId = model.CitizenId;
                doctor.PracticeLicense = model.PracticeLicense;
                doctor.DateOfBirth = model.DateOfBirth;
                doctor.Sex = model.Sex;
                doctor.IsOnShift = model.IsOnShift;
                _context.Doctors.Update(doctor);

                await _context.SaveChangesAsync();
                await _auditLogService.LogAsync(
                    "Update",
                    "Doctor",
                    doctor.Id.ToString(),
                    $"Cập nhật bác sĩ {oldFullName} -> {model.FullName}; email {oldEmail} -> {model.Email}; chuyên khoa {oldSpecialty} -> {model.Specialty}; trạng thái trực {oldShiftStatus} -> {model.IsOnShift}.",
                    doctor.UserId,
                    model.FullName);

                await transaction.CommitAsync();

                TempData["Success"] = "Cập nhật thông tin bác sĩ thành công.";
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
                var willLock = !user.IsDeleted;
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
                await _auditLogService.LogAsync(
                    willLock ? "Lock" : "Unlock",
                    "DoctorAccount",
                    user.Id.ToString(),
                    willLock
                        ? $"Khóa tài khoản bác sĩ {user.FullName}. Lý do: {user.LockReason}"
                        : $"Mở khóa tài khoản bác sĩ {user.FullName}.",
                    user.Id,
                    user.FullName);

                TempData["Success"] = user.IsDeleted ? "Đã khóa tài khoản bác sĩ." : "Đã mở khóa tài khoản bác sĩ.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
