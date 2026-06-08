using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;
        private readonly ITwilioVerifyService _twilioVerify;

        public HomeController(ILogger<HomeController> logger, SmartHealthMonitoringContext context, IEmailService emailService, ITwilioVerifyService twilioVerify)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _twilioVerify = twilioVerify;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Contact()
        {
            var numberOfDoctor = _context.Doctors.Count();
            var numberOfPatient = _context.Patients.Count();

            ViewBag.NumberOfDoctor = numberOfDoctor;
            ViewBag.NumberOfPatient = numberOfPatient;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(
    ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string html = $@"
        <h2>Liên hệ mới từ SmartHealth</h2>

        <p>
            <b>Họ tên:</b>
            {model.FullName}
        </p>

        <p>
            <b>Email:</b>
            {model.Email}
        </p>

        <p>
            <b>Điện thoại:</b>
            {model.Phone}
        </p>

        <p>
            <b>Nội dung:</b>
        </p>

        <div>
            {model.Message}
        </div>";

            await _emailService.SendEmailAsync("namntp27@gmail.com","Liên hệ mới từ website", html);

            TempData["Success"] = "Gửi thành công. Chúng tôi sẽ phản hồi trong vòng 24 giờ.";


            return RedirectToAction(nameof(Contact));
        }

        // ==========================================
        // GET: /Home/Profile
        // ==========================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null) return RedirectToAction("Login", "Auth");

            var vm = new ProfileViewModel
            {
                UserId    = user.Id,
                FullName  = user.FullName,
                Email     = user.Email,
                CreatedAt = user.CreatedAt,
                IsGoogleAccount = string.IsNullOrEmpty(user.PasswordHash),
                Role      = user.Role
            };

            // Nếu là Patient thì lấy thêm thông tin
            if (user.Role == 0)
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
                if (patient != null)
                {
                    vm.PatientId       = patient.Id;
                    vm.DateOfBirth     = patient.DateOfBirth;
                    vm.Sex             = patient.Sex;
                    vm.Phone           = patient.Phone;
                    vm.IsPhoneVerified = patient.IsPhoneVerified;
                    vm.Address         = patient.Address;
                    vm.CitizenId       = patient.CitizenId;

                    // Thống kê nhanh
                    vm.TotalVitalLogs       = await _context.DailyVitalLogs.CountAsync(v => v.PatientId == patient.Id);
                    vm.TotalClinicalRecords = await _context.ClinicalRecords.CountAsync(c => c.PatientId == patient.Id);
                    vm.TotalWarningAlerts   = await _context.WarningAlerts.CountAsync(w => w.PatientId == patient.Id && !w.IsDeleted);
                    vm.LastLogAt = await _context.DailyVitalLogs
                        .Where(v => v.PatientId == patient.Id)
                        .OrderByDescending(v => v.LoggedAt)
                        .Select(v => (DateTime?)v.LoggedAt)
                        .FirstOrDefaultAsync();
                }
            }
            // Nếu là Doctor thì lấy thêm thông tin
            else if (user.Role == 1)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor != null)
                {
                    vm.CitizenId       = doctor.CitizenId;
                    vm.PracticeLicense = doctor.PracticeLicense;
                    vm.Specialty       = doctor.Specialty;
                    vm.Phone           = doctor.Phone;
                    vm.Address         = doctor.Address;
                    vm.IsPhoneVerified = doctor.IsPhoneVerified;
                    vm.DateOfBirth     = doctor.DateOfBirth;
                    vm.Sex             = doctor.Sex;
                }
            }

            return View(vm);
        }

        // ==========================================
        // POST: /Home/UpdateProfile
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null) return RedirectToAction("Login", "Auth");

            // Kiểm tra validation bổ sung theo vai trò
            if (user.Role == 0) // Patient
            {
                if (model.DateOfBirth == null)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "Vui lòng chọn ngày sinh.");
                if (model.Sex == null)
                    ModelState.AddModelError(nameof(model.Sex), "Vui lòng chọn giới tính.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ. Vui lòng kiểm tra lại.";
                return RedirectToAction(nameof(Profile));
            }

            // Cập nhật tên trong bảng Users (chỉ dành cho Bệnh nhân)
            if (user.Role == 0)
            {
                user.FullName = model.FullName;
                _context.Users.Update(user);
            }

            // Cập nhật thông tin Patient
            if (user.Role == 0)
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
                if (patient != null)
                {
                    // Nếu SĐT thay đổi thì reset xác thực
                    if (patient.Phone != model.Phone)
                        patient.IsPhoneVerified = false;

                    patient.DateOfBirth = model.DateOfBirth!.Value;
                    patient.Sex         = model.Sex!.Value;
                    patient.Phone       = model.Phone;
                    patient.Address     = model.Address;
                    patient.CitizenId   = model.CitizenId;
                    _context.Patients.Update(patient);
                }
            }
            // Cập nhật thông tin Doctor
            else if (user.Role == 1)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor != null)
                {
                    // Nếu SĐT thay đổi thì reset xác thực
                    if (doctor.Phone != model.Phone)
                        doctor.IsPhoneVerified = false;

                    doctor.Phone = model.Phone;
                    doctor.Address = model.Address;
                    _context.Doctors.Update(doctor);
                }
            }

            await _context.SaveChangesAsync();

            // Cập nhật lại claim FullName trong cookie
            var claims = User.Claims
                .Where(c => c.Type != "FullName")
                .ToList();
            claims.Add(new Claim("FullName", user.FullName));

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                new AuthenticationProperties { IsPersistent = false });

            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToAction(nameof(Profile));
        }

        // ==========================================
        // POST: /Home/ChangePassword
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Gộp tất cả lỗi validation thành 1 chuỗi
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                TempData["PwdError"] = string.Join(" | ", errors);
                return RedirectToAction(nameof(Profile), new { tab = "security" });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null) return RedirectToAction("Login", "Auth");

            // Không cho tài khoản Google đổi mật khẩu
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                TempData["PwdError"] = "Tài khoản Google không thể đổi mật khẩu tại đây.";
                return RedirectToAction(nameof(Profile), new { tab = "security" });
            }

            // Kiểm tra mật khẩu hiện tại
            bool isCurrentValid = false;
            if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$"))
                isCurrentValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash);
            else
                isCurrentValid = (model.CurrentPassword == user.PasswordHash); // Fallback seed data

            if (!isCurrentValid)
            {
                TempData["PwdError"] = "Mật khẩu hiện tại không đúng.";
                return RedirectToAction(nameof(Profile), new { tab = "security" });
            }

            // Không được dùng lại mật khẩu cũ
            bool isSameAsOld;
            if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || user.PasswordHash.StartsWith("$2y$"))
                isSameAsOld = BCrypt.Net.BCrypt.Verify(model.NewPassword, user.PasswordHash);
            else
                isSameAsOld = (model.NewPassword == user.PasswordHash); // Fallback seed data

            if (isSameAsOld)
            {
                TempData["PwdError"] = "Mật khẩu mới không được trùng với mật khẩu hiện tại.";
                return RedirectToAction(nameof(Profile), new { tab = "security" });
            }

            // Hash và lưu mật khẩu mới
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["PwdSuccess"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile), new { tab = "security" });
        }

        // ==========================================
        // POST: /Home/SendPhoneOtp  — Gửi mã OTP xác thực SĐT
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPhoneOtp([FromForm] string phone)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "Phiên đăng nhập không hợp lệ." });

            if (string.IsNullOrWhiteSpace(phone) ||
                !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^(0|\+84)[0-9]{9}$"))
                return Json(new { success = false, message = "Số điện thoại không hợp lệ." });

            // Lưu số điện thoại vào Session để VerifyPhoneOtp biết số nào cần kiểm tra
            HttpContext.Session.SetString($"PhoneOtpTarget_{userId}", phone);

            // Twilio Verify tự sinh OTP và gửi SMS
            var sent = await _twilioVerify.SendOtpAsync(phone);

            if (!sent)
                return Json(new { success = false, message = "Không thể gửi SMS. Vui lòng thử lại sau." });

            return Json(new { success = true, message = $"Mã OTP đã gửi đến {phone}. Hiệu lực 10 phút." });
        }

        // ==========================================
        // POST: /Home/VerifyPhoneOtp  — Xác nhận mã OTP
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPhoneOtp([FromForm] string otp)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "Phiên đăng nhập không hợp lệ." });

            var storedPhone = HttpContext.Session.GetString($"PhoneOtpTarget_{userId}");

            if (string.IsNullOrEmpty(storedPhone))
                return Json(new { success = false, message = "Phiên xác thực đã hết hạn. Vui lòng gửi lại mã OTP." });

            // Twilio Verify tự kiểm tra mã OTP
            var approved = await _twilioVerify.VerifyOtpAsync(storedPhone, otp.Trim());

            if (!approved)
                return Json(new { success = false, message = "Mã OTP không chính xác hoặc đã hết hạn." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy người dùng." });

            if (user.Role == 0)
            {
                var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
                if (patient == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ bệnh nhân." });

                patient.Phone = storedPhone;
                patient.IsPhoneVerified = true;
                _context.Patients.Update(patient);
            }
            else if (user.Role == 1)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

                doctor.Phone = storedPhone;
                doctor.IsPhoneVerified = true;
                _context.Doctors.Update(doctor);
            }
            else
            {
                return Json(new { success = false, message = "Vai trò không hỗ trợ xác thực số điện thoại." });
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove($"PhoneOtpTarget_{userId}");

            return Json(new { success = true, message = "Xác thực số điện thoại thành công!" });
        }
    }
}

