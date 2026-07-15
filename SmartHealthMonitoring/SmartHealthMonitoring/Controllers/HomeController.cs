using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;
        private readonly ITwilioVerifyService _twilioVerify;
        private readonly IMinioService _minioService;
        private readonly GeminiService _geminiService;

        public HomeController(ILogger<HomeController> logger, SmartHealthMonitoringContext context, IEmailService emailService, ITwilioVerifyService twilioVerify, IMinioService minioService, GeminiService geminiService)
        {
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _twilioVerify = twilioVerify;
            _minioService = minioService;
            _geminiService = geminiService;
        }

        public async Task<IActionResult> Index()
        {
            var publishedNews = await _context.HealthNewsPosts
                .Where(n => n.Status == "Published")
                .OrderByDescending(n => n.PublishedAt)
                .Take(9)
                .ToListAsync();

            ViewBag.HealthNews = publishedNews;
            return View();
        }

        // ==========================================
        // GET: /Home/News
        // ==========================================
        public async Task<IActionResult> News(string? keyword, int page = 1)
        {
            int pageSize = 6;
            var query = _context.HealthNewsPosts.Where(n => n.Status == "Published");

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(n => n.Title.Contains(keyword) || n.Summary.Contains(keyword));
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var newsList = await query
                .OrderByDescending(n => n.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Keyword = keyword;

            return View(newsList);
        }

        // ==========================================
        // GET: /Home/NewsDetail/{id}
        // ==========================================
        public async Task<IActionResult> NewsDetail(int id)
        {
            var news = await _context.HealthNewsPosts
                .FirstOrDefaultAsync(n => n.Id == id && n.Status == "Published");

            if (news == null)
            {
                return NotFound();
            }

            var relatedNews = await _context.HealthNewsPosts
                .Where(n => n.Status == "Published" && n.Id != id)
                .OrderByDescending(n => n.PublishedAt)
                .Take(5)
                .ToListAsync();

            ViewBag.RelatedNews = relatedNews;

            return View(news);
        }

        [Authorize(Roles = "0")]
        public async Task<IActionResult> Habits()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null && user.Role == 0) // Patient
                {
                    var patient = await _context.Patients
                        .Include(p => p.PatientHabit)
                        .FirstOrDefaultAsync(p => p.UserId == userId);

                    if (patient != null)
                    {
                        var habitViewModel = new HabitViewModel();
                        if (patient.PatientHabit != null)
                        {
                            var h = patient.PatientHabit;
                            habitViewModel.DietSalty = h.DietSalty;
                            habitViewModel.DietHighFat = h.DietHighFat;
                            habitViewModel.DietHighSugar = h.DietHighSugar;
                            habitViewModel.DietLowFiber = h.DietLowFiber;
                            habitViewModel.AlcoholHeavy = h.AlcoholHeavy;
                            habitViewModel.CaffeineSpike = h.CaffeineSpike;
                            habitViewModel.LifestyleSedentary = h.LifestyleSedentary;
                            habitViewModel.LifestyleSitLong = h.LifestyleSitLong;
                            habitViewModel.SleepDeprived = h.SleepDeprived;
                            habitViewModel.NoHealthCheck = h.NoHealthCheck;
                            habitViewModel.SmokeActive = h.SmokeActive;
                            habitViewModel.SmokePassive = h.SmokePassive;
                            habitViewModel.SelfMedication = h.SelfMedication;
                            habitViewModel.StressHigh = h.StressHigh;
                            habitViewModel.ExerciseRegularly = h.ExerciseRegularly;
                            habitViewModel.SleepEarly = h.SleepEarly;
                            habitViewModel.DrinkEnoughWater = h.DrinkEnoughWater;
                            habitViewModel.DietBalanced = h.DietBalanced;
                            habitViewModel.RegularHealthCheck = h.RegularHealthCheck;
                            habitViewModel.NoSubstanceAbuse = h.NoSubstanceAbuse;
                        }

                        return View(habitViewModel);
                    }
                }
            }
            return View(new HabitViewModel());
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
                Role      = user.Role,
                AvatarUrl = user.AvatarUrl
            };

            // Nếu là Patient thì lấy thêm thông tin
            if (user.Role == 0)
            {
                var patient = await _context.Patients
                    .Include(p => p.PatientHabit)
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
                if (patient != null)
                {
                    vm.PatientId       = patient.Id;
                    vm.DateOfBirth     = patient.DateOfBirth;
                    vm.Sex             = patient.Sex;
                    vm.Phone           = patient.Phone;
                    vm.IsPhoneVerified = patient.IsPhoneVerified;
                    vm.Address         = patient.Address;
                    vm.CitizenId       = patient.CitizenId;

                    // Lấy link ảnh CCCD 2 mặt từ MinIO cho bệnh nhân nếu đã có CitizenId
                    if (!string.IsNullOrEmpty(patient.CitizenId))
                    {
                        var frontKey = $"cccd-front-{patient.Id}";
                        var backKey = $"cccd-back-{patient.Id}";
                        vm.CitizenIdFrontUrl = await _minioService.GetPresignedUrlAsync("smarthealth-cccds", frontKey, 10080);
                        vm.CitizenIdBackUrl = await _minioService.GetPresignedUrlAsync("smarthealth-cccds", backKey, 10080);
                    }

                    // Thống kê nhanh
                    vm.TotalVitalLogs       = await _context.DailyVitalLogs.CountAsync(v => v.PatientId == patient.Id);
                    vm.TotalClinicalRecords = await _context.ClinicalRecords.CountAsync(c => c.PatientId == patient.Id);
                    vm.TotalWarningAlerts   = await _context.WarningAlerts.CountAsync(w => w.PatientId == patient.Id && !w.IsDeleted);
                    vm.LastLogAt = await _context.DailyVitalLogs
                        .Where(v => v.PatientId == patient.Id)
                        .OrderByDescending(v => v.LoggedAt)
                        .Select(v => (DateTime?)v.LoggedAt)
                        .FirstOrDefaultAsync();

                    // Load thói quen sinh hoạt (nếu có)
                    if (patient.PatientHabit != null)
                    {
                        var h = patient.PatientHabit;
                        vm.Habit = new SmartHealthMonitoring.ViewModels.HabitViewModel
                        {
                            DietSalty          = h.DietSalty,
                            DietHighFat        = h.DietHighFat,
                            DietHighSugar      = h.DietHighSugar,
                            DietLowFiber       = h.DietLowFiber,
                            AlcoholHeavy       = h.AlcoholHeavy,
                            CaffeineSpike      = h.CaffeineSpike,
                            LifestyleSedentary = h.LifestyleSedentary,
                            LifestyleSitLong   = h.LifestyleSitLong,
                            SleepDeprived      = h.SleepDeprived,
                            NoHealthCheck      = h.NoHealthCheck,
                            SmokeActive        = h.SmokeActive,
                            SmokePassive       = h.SmokePassive,
                            SelfMedication     = h.SelfMedication,
                            StressHigh         = h.StressHigh,
                            ExerciseRegularly  = h.ExerciseRegularly,
                            SleepEarly         = h.SleepEarly,
                            DrinkEnoughWater   = h.DrinkEnoughWater,
                            DietBalanced       = h.DietBalanced,
                            RegularHealthCheck = h.RegularHealthCheck,
                            NoSubstanceAbuse   = h.NoSubstanceAbuse,
                        };
                    }
                    else
                    {
                        vm.Habit = new SmartHealthMonitoring.ViewModels.HabitViewModel(); // form trống
                    }
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

                    // Lấy link ảnh CCCD 2 mặt từ MinIO cho bác sĩ nếu đã có CitizenId
                    if (!string.IsNullOrEmpty(doctor.CitizenId))
                    {
                        var frontKey = $"cccd-front-{doctor.Id}";
                        var backKey = $"cccd-back-{doctor.Id}";
                        vm.CitizenIdFrontUrl = await _minioService.GetPresignedUrlAsync("smarthealth-cccds", frontKey, 10080);
                        vm.CitizenIdBackUrl = await _minioService.GetPresignedUrlAsync("smarthealth-cccds", backKey, 10080);
                    }
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
            if (user.Role == 0 || user.Role == 1) // Patient or Doctor
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

            // Cập nhật tên trong bảng Users (Bệnh nhân và Bác sĩ)
            if (user.Role == 0 || user.Role == 1)
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

                    // Xử lý upload ảnh CCCD mặt trước
                    if (model.CitizenIdFrontFile != null && model.CitizenIdFrontFile.Length > 0)
                    {
                        var frontKey = $"cccd-front-{patient.Id}";
                        using var stream = model.CitizenIdFrontFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", frontKey, stream, model.CitizenIdFrontFile.ContentType);
                    }

                    // Xử lý upload ảnh CCCD mặt sau
                    if (model.CitizenIdBackFile != null && model.CitizenIdBackFile.Length > 0)
                    {
                        var backKey = $"cccd-back-{patient.Id}";
                        using var stream = model.CitizenIdBackFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", backKey, stream, model.CitizenIdBackFile.ContentType);
                    }

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

                    doctor.DateOfBirth = model.DateOfBirth!.Value;
                    doctor.Sex         = model.Sex!.Value;
                    doctor.Phone       = model.Phone;
                    doctor.Address     = model.Address;
                    doctor.CitizenId   = model.CitizenId;

                    // Xử lý upload ảnh CCCD mặt trước
                    if (model.CitizenIdFrontFile != null && model.CitizenIdFrontFile.Length > 0)
                    {
                        var frontKey = $"cccd-front-{doctor.Id}";
                        using var stream = model.CitizenIdFrontFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", frontKey, stream, model.CitizenIdFrontFile.ContentType);
                    }

                    // Xử lý upload ảnh CCCD mặt sau
                    if (model.CitizenIdBackFile != null && model.CitizenIdBackFile.Length > 0)
                    {
                        var backKey = $"cccd-back-{doctor.Id}";
                        using var stream = model.CitizenIdBackFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", backKey, stream, model.CitizenIdBackFile.ContentType);
                    }

                    _context.Doctors.Update(doctor);
                }
            }

            // Xử lý upload ảnh đại diện (Avatar)
            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                var bucketName = "smarthealth-avatars";
                var objectName = $"avatar-{user.Id}-{Guid.NewGuid()}{System.IO.Path.GetExtension(model.AvatarFile.FileName)}";
                
                using (var stream = model.AvatarFile.OpenReadStream())
                {
                    await _minioService.UploadFileAsync(bucketName, objectName, stream, model.AvatarFile.ContentType);
                }
                
                user.AvatarUrl = objectName;
                // Đảm bảo User entity được đánh dấu là thay đổi nếu chưa được Update() ở trên
                if (user.Role == 1 || user.Role == 2) 
                {
                    _context.Users.Update(user);
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
        // POST: /Home/ScanCccd — OCR ảnh CCCD qua Gemini API
        // ==========================================
        [HttpPost("/Home/ScanCccd")]
        [Authorize]
        public async Task<IActionResult> ScanCccd(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn hoặc chụp ảnh mặt trước CCCD." });
            }

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var imageBytes = ms.ToArray();

                var jsonResult = await _geminiService.ScanCitizenIdAsync(imageBytes, file.ContentType);
                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scan CCCD image using Gemini");
                return StatusCode(500, new { success = false, message = "Không thể trích xuất được thông tin từ ảnh này. Vui lòng chọn ảnh rõ nét hơn hoặc tự điền." });
            }
        }

        // ==========================================
        // POST: /Home/UpdateHabits — Cập nhật thói quen sinh hoạt
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateHabits(HabitViewModel model, string source = "")
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var patient = await _context.Patients
                .Include(p => p.PatientHabit)
                .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

            if (patient == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ bệnh nhân.";
                return RedirectToAction(nameof(Profile));
            }

            if (patient.PatientHabit == null)
            {
                // Chưa có record → tạo mới
                var habit = new PatientHabit
                {
                    PatientId          = patient.Id,
                    DietSalty          = model.DietSalty,
                    DietHighFat        = model.DietHighFat,
                    DietHighSugar      = model.DietHighSugar,
                    DietLowFiber       = model.DietLowFiber,
                    AlcoholHeavy       = model.AlcoholHeavy,
                    CaffeineSpike      = model.CaffeineSpike,
                    LifestyleSedentary = model.LifestyleSedentary,
                    LifestyleSitLong   = model.LifestyleSitLong,
                    SleepDeprived      = model.SleepDeprived,
                    NoHealthCheck      = model.NoHealthCheck,
                    SmokeActive        = model.SmokeActive,
                    SmokePassive       = model.SmokePassive,
                    SelfMedication     = model.SelfMedication,
                    StressHigh         = model.StressHigh,
                    ExerciseRegularly  = model.ExerciseRegularly,
                    SleepEarly         = model.SleepEarly,
                    DrinkEnoughWater   = model.DrinkEnoughWater,
                    DietBalanced       = model.DietBalanced,
                    RegularHealthCheck = model.RegularHealthCheck,
                    NoSubstanceAbuse   = model.NoSubstanceAbuse,
                    UpdatedAt          = DateTime.UtcNow,
                };
                _context.PatientHabits.Add(habit);
            }
            else
            {
                // Đã có → cập nhật
                var h = patient.PatientHabit;
                h.DietSalty          = model.DietSalty;
                h.DietHighFat        = model.DietHighFat;
                h.DietHighSugar      = model.DietHighSugar;
                h.DietLowFiber       = model.DietLowFiber;
                h.AlcoholHeavy       = model.AlcoholHeavy;
                h.CaffeineSpike      = model.CaffeineSpike;
                h.LifestyleSedentary = model.LifestyleSedentary;
                h.LifestyleSitLong   = model.LifestyleSitLong;
                h.SleepDeprived      = model.SleepDeprived;
                h.NoHealthCheck      = model.NoHealthCheck;
                h.SmokeActive        = model.SmokeActive;
                h.SmokePassive       = model.SmokePassive;
                h.SelfMedication     = model.SelfMedication;
                h.StressHigh         = model.StressHigh;
                h.ExerciseRegularly  = model.ExerciseRegularly;
                h.SleepEarly         = model.SleepEarly;
                h.DrinkEnoughWater   = model.DrinkEnoughWater;
                h.DietBalanced       = model.DietBalanced;
                h.RegularHealthCheck = model.RegularHealthCheck;
                h.NoSubstanceAbuse   = model.NoSubstanceAbuse;
                h.UpdatedAt          = DateTime.UtcNow;
                _context.PatientHabits.Update(h);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật thói quen sinh hoạt thành công!";
            
            if (source == "home")
            {
                return RedirectToAction(nameof(Index));
            }
            if (source == "habits")
            {
                return RedirectToAction(nameof(Habits));
            }
            return RedirectToAction(nameof(Profile), new { tab = "habits" });
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

        [Route("Home/Error")]
        [AllowAnonymous]
        public IActionResult Error()
        {
            var exceptionHandlerPathFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;
            
            ViewBag.ErrorMessage = exception?.Message;
            ViewBag.StackTrace = exception?.StackTrace;
            ViewBag.InnerException = exception?.InnerException?.Message;
            
            return View();
        }
        // ==========================================
        // POST: /Home/UploadAvatar
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            if (avatarFile != null && avatarFile.Length > 0)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    var bucketName = "smarthealth-avatars";
                    var objectName = $"avatar-{user.Id}-{Guid.NewGuid()}{System.IO.Path.GetExtension(avatarFile.FileName)}";
                    
                    using (var stream = avatarFile.OpenReadStream())
                    {
                        await _minioService.UploadFileAsync(bucketName, objectName, stream, avatarFile.ContentType);
                    }
                    
                    user.AvatarUrl = objectName;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = "Cập nhật ảnh đại diện thành công!";
                }
            }
            return RedirectToAction(nameof(Profile));
        }

        // ==========================================
        // GET: /Avatar/{userId}
        // ==========================================
        [HttpGet("/Avatar/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvatar(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || string.IsNullOrEmpty(user.AvatarUrl))
            {
                return Redirect("/assets/images/default-avatar.png"); // or wherever the default avatar is
            }

            try
            {
                var presignedUrl = await _minioService.GetPresignedUrlAsync("smarthealth-avatars", user.AvatarUrl, 10080);
                return Redirect(presignedUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting presigned URL for avatar");
                return Redirect("/assets/images/default-avatar.png");
            }
        }
    }
}

