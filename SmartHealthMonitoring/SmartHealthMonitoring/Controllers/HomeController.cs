using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Home;
using SmartHealthMonitoring.Interfaces.Patient;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Interfaces.Minio;
using SmartHealthMonitoring.Interfaces.Notification;
using SmartHealthMonitoring.Interfaces.QR;
using SmartHealthMonitoring.Interfaces;
using System.IO;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProfileService _profileService;
        private readonly IEmailService _emailService;
        private readonly ITwilioVerifyService _twilioVerify;
        private readonly IMinioService _minioService;
        private readonly ILocalOcrService _localOcrService;

        public HomeController(
            ILogger<HomeController> logger,
            IProfileService profileService,
            IEmailService emailService,
            ITwilioVerifyService twilioVerify,
            IMinioService minioService,
            ILocalOcrService localOcrService)
        {
            _logger = logger;
            _profileService = profileService;
            _emailService = emailService;
            _twilioVerify = twilioVerify;
            _minioService = minioService;
            _localOcrService = localOcrService;
        }

        public async Task<IActionResult> Index()
        {
            var publishedNews = await _profileService.GetPublishedNewsAsync(9);
            ViewBag.HealthNews = publishedNews;
            return View();
        }

        public async Task<IActionResult> News(string? keyword, int page = 1)
        {
            int pageSize = 6;
            var (newsList, totalItems) = await _profileService.GetNewsPagedAsync(keyword, page, pageSize);

            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Keyword = keyword;

            return View(newsList);
        }

        public async Task<IActionResult> NewsDetail(int id)
        {
            var (news, relatedNews) = await _profileService.GetNewsDetailAsync(id);

            if (news == null)
            {
                return NotFound();
            }

            ViewBag.RelatedNews = relatedNews;
            return View(news);
        }

        [Authorize(Roles = "0")]
        public async Task<IActionResult> Habits()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var habits = await _profileService.GetHabitsAsync(userId);
                if (habits != null)
                {
                    return View(habits);
                }
            }
            return View(new HabitViewModel());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> Contact()
        {
            var numberOfDoctor = await _profileService.GetDoctorCountAsync();
            var numberOfPatient = await _profileService.GetPatientCountAsync();

            ViewBag.NumberOfDoctor = numberOfDoctor;
            ViewBag.NumberOfPatient = numberOfPatient;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string html = $@"
        <h2>Li�n h? m?i t? SmartHealth</h2>
        <p><b>H? t�n:</b> {model.FullName}</p>
        <p><b>Email:</b> {model.Email}</p>
        <p><b>�i?n tho?i:</b> {model.Phone}</p>
        <p><b>N?i dung:</b></p>
        <div>{model.Message}</div>";

            await _emailService.SendEmailAsync("namntp27@gmail.com","Li�n h? m?i t? website", html);
            TempData["Success"] = "G?i th�nh c�ng. Ch�ng t�i s? ph?n h?i trong v�ng 24 gi?.";
            return RedirectToAction(nameof(Contact));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var user = await _profileService.GetUserByIdAsync(userId);
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

            if (user.Role == 0)
            {
                var patient = await _profileService.GetPatientByUserIdAsync(userId);
                if (patient != null)
                {
                    vm.PatientId       = patient.Id;
                    vm.DateOfBirth     = patient.DateOfBirth;
                    vm.Sex             = patient.Sex;
                    vm.Phone           = patient.Phone;
                    vm.IsPhoneVerified = patient.IsPhoneVerified;
                    vm.Address         = patient.Address;
                    vm.CitizenId       = patient.CitizenId;

                    if (!string.IsNullOrEmpty(patient.CitizenId))
                    {
                        var frontKey = $"cccd-front-{patient.Id}";
                        var backKey = $"cccd-back-{patient.Id}";
                        vm.CitizenIdFrontUrl = await _minioService.GetPresignedUrlAsync("smarthealth-cccds", frontKey, 10080);
                        vm.CitizenIdBackUrl = await _minioService.GetPresignedUrlAsync("smarthealth-cccds", backKey, 10080);
                    }

                    var stats = await _profileService.GetProfileStatsAsync(userId);
                    vm.TotalVitalLogs       = stats.DailyLogCount;
                    vm.TotalClinicalRecords = stats.ClinicalRecordCount;
                    vm.TotalWarningAlerts   = stats.AlertCount;
                    vm.LastLogAt            = stats.LastLogDate;

                    var habits = await _profileService.GetHabitsAsync(userId);
                    vm.Habit = habits ?? new HabitViewModel();
                }
            }
            else if (user.Role == 1)
            {
                var doctor = await _profileService.GetDoctorByUserIdAsync(userId);
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var user = await _profileService.GetUserByIdAsync(userId);
            if (user == null) return RedirectToAction("Login", "Auth");

            if (user.Role == 0 || user.Role == 1)
            {
                if (model.DateOfBirth == null)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "Vui l�ng ch?n ng�y sinh.");
                if (model.Sex == null)
                    ModelState.AddModelError(nameof(model.Sex), "Vui l�ng ch?n gi?i t�nh.");
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Th�ng tin kh�ng h?p l?. Vui l�ng ki?m tra l?i.";
                return RedirectToAction(nameof(Profile));
            }

            await _profileService.UpdateProfileAsync(userId, model);

            if (user.Role == 0)
            {
                var patient = await _profileService.GetPatientByUserIdAsync(userId);
                if (patient != null)
                {
                    if (model.CitizenIdFrontFile != null && model.CitizenIdFrontFile.Length > 0)
                    {
                        var frontKey = $"cccd-front-{patient.Id}";
                        using var stream = model.CitizenIdFrontFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", frontKey, stream, model.CitizenIdFrontFile.ContentType);
                    }
                    if (model.CitizenIdBackFile != null && model.CitizenIdBackFile.Length > 0)
                    {
                        var backKey = $"cccd-back-{patient.Id}";
                        using var stream = model.CitizenIdBackFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", backKey, stream, model.CitizenIdBackFile.ContentType);
                    }
                }
            }
            else if (user.Role == 1)
            {
                var doctor = await _profileService.GetDoctorByUserIdAsync(userId);
                if (doctor != null)
                {
                    if (model.CitizenIdFrontFile != null && model.CitizenIdFrontFile.Length > 0)
                    {
                        var frontKey = $"cccd-front-{doctor.Id}";
                        using var stream = model.CitizenIdFrontFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", frontKey, stream, model.CitizenIdFrontFile.ContentType);
                    }
                    if (model.CitizenIdBackFile != null && model.CitizenIdBackFile.Length > 0)
                    {
                        var backKey = $"cccd-back-{doctor.Id}";
                        using var stream = model.CitizenIdBackFile.OpenReadStream();
                        await _minioService.UploadFileAsync("smarthealth-cccds", backKey, stream, model.CitizenIdBackFile.ContentType);
                    }
                }
            }

            TempData["SuccessMessage"] = "C?p nh?t th�ng tin th�nh c�ng.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize(Roles = "0")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateHabits(HabitViewModel model)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            await _profileService.UpdateHabitsAsync(userId, model);

            TempData["SuccessMessage"] = "C?p nh?t th�i quen sinh ho?t th�nh c�ng.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Th�ng tin d?i m?t kh?u kh�ng h?p l?.";
                return RedirectToAction(nameof(Profile));
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Auth");

            var user = await _profileService.GetUserByIdAsync(userId);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                TempData["ErrorMessage"] = "T�i kho?n kh�ng th? d?i m?t kh?u.";
                return RedirectToAction(nameof(Profile));
            }

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
            {
                TempData["ErrorMessage"] = "M?t kh?u hi?n t?i kh�ng d�ng.";
                return RedirectToAction(nameof(Profile));
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            await _profileService.ChangePasswordAsync(userId, newHash);

            TempData["SuccessMessage"] = "�?i m?t kh?u th�nh c�ng. Vui l�ng dang nh?p l?i.";
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GeneratePhoneOtp([FromBody] GenerateOtpRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "Kh�ng x�c d?nh du?c ngu?i d�ng." });

            var result = await _twilioVerify.SendOtpAsync(request.Phone);
            if (result)
            {
                return Json(new { success = true, message = "M� OTP d� du?c g?i." });
            }
            return Json(new { success = false, message = "L?i x? l? y?u c?u" });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPhoneOtp([FromBody] VerifyOtpRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "Kh�ng x�c d?nh du?c ngu?i d�ng." });

            var result = await _twilioVerify.VerifyOtpAsync(request.Phone, request.Code);
            if (result)
            {
                await _profileService.UpdatePhoneAsync(userId, request.Phone);
                return Json(new { success = true, message = "X�c th?c s? di?n tho?i th�nh c�ng." });
            }
            return Json(new { success = false, message = "L?i x? l? y?u c?u" });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return Json(new { success = false, message = "Kh�ng c� file n�o du?c ch?n." });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, message = "Unauthorized" });

            try
            {
                var objectName = $"avatar_{userId}_{Guid.NewGuid()}{Path.GetExtension(avatarFile.FileName)}";
                using var stream = avatarFile.OpenReadStream();
                await _minioService.UploadFileAsync("smarthealth-avatars", objectName, stream, avatarFile.ContentType);

                await _profileService.UpdateAvatarAsync(userId, objectName);

                var presignedUrl = await _minioService.GetPresignedUrlAsync("smarthealth-avatars", objectName, 10080);
                return Json(new { success = true, url = presignedUrl, message = "C?p nh?t ?nh d?i di?n th�nh c�ng." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading avatar");
                return Json(new { success = false, message = "L?i khi upload ?nh." });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAvatar()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return Json(new { success = false, url = "/images/default-avatar.png" });

            var avatarUrl = await _profileService.GetAvatarUrlAsync(userId);
            if (string.IsNullOrEmpty(avatarUrl))
                return Json(new { success = true, url = "/images/default-avatar.png" });

            var presignedUrl = await _minioService.GetPresignedUrlAsync("smarthealth-avatars", avatarUrl, 10080);
            return Json(new { success = true, url = presignedUrl });
        }
        
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessOcr([FromForm] IFormFile cccdImage)
        {
            if (cccdImage == null || cccdImage.Length == 0)
                return Json(new { success = false, message = "Kh�ng c� ?nh" });

            using var ms = new System.IO.MemoryStream();
            await cccdImage.CopyToAsync(ms);
            var result = await _localOcrService.ScanCitizenIdAsync(ms.ToArray());
            return Json(result);
        }
    }
}




