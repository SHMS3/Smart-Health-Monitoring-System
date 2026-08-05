using SmartHealthMonitoring.Interfaces.Audit;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Interfaces.Doctor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.Models;

using SmartHealthMonitoring.Interfaces.AI;
using SmartHealthMonitoring.ViewModels;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers.AI
{
    [Authorize(Roles = "1")]
    public class WarningAlertController : Controller
    {
        private readonly IAiWarningAlertService _warningAlertService;
        private readonly IEmailService _emailService;
        private readonly IDoctorService _doctorService;
        private readonly IEmailTriggerService _emailTriggerService;
        private readonly IAuditLogService _auditLogService;
        private readonly IAnfisExplainabilityService _explainabilityService;
        private readonly IThresholdService _thresholdService;


        public WarningAlertController(
            IAiWarningAlertService warningAlertService,
            IDoctorService doctorService,
            IEmailService emailService,
            IEmailTriggerService emailTriggerService,
            IAuditLogService auditLogService,
            IAnfisExplainabilityService explainabilityService,
            IThresholdService thresholdService)
        {
            _warningAlertService = warningAlertService;
            _doctorService = doctorService;
            _emailService = emailService;
            _emailTriggerService = emailTriggerService;
            _auditLogService = auditLogService;
            _explainabilityService = explainabilityService;
            _thresholdService = thresholdService;
        }


        public async Task<IActionResult> Dashboard(byte? status, string? keyword, int page = 1, int pageSize = 10, bool onlyMyAlerts = false)
        {
            int? doctorId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                doctorId = doctor?.Id;
            }

            int? filterDoctorId = onlyMyAlerts ? doctorId : null;

            var totalRecords = await _warningAlertService.GetTotalAlertsAsync(status, keyword, filterDoctorId);
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var alerts = await _warningAlertService.GetAlertsAsync(
                status,
                keyword,
                page,
                pageSize,
                filterDoctorId);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.OnlyMyAlerts = onlyMyAlerts;

            ViewData["DoctorId"] = doctorId;
            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalItems"] = totalRecords;
            ViewData["PageSize"] = pageSize;
            ViewData["CurrentStatus"] = status?.ToString() ?? "";

            return View(alerts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim(int id)
        {
            var userIdString =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new
                {
                    success = false,
                    message = "Unauthorized"
                });
            }

            var doctor =
                await _doctorService.GetDoctorByUserIdAsync(
                    int.Parse(userIdString));

            if (doctor == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Doctor not found"
                });
            }

            var result =
                await _warningAlertService
                    .ClaimAlertAsync(id, doctor.Id);

            return Json(new
            {
                success = result.Success,
                message = result.Message
            });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(
    int id,
    string resolutionNote,
    bool sendEmailInvitation = false,
    DateTime? appointmentDate = null,
    short? systolicBpWarning = null,
    short? systolicBpDanger = null,
    short? diastolicBpWarning = null,
    short? diastolicBpDanger = null,
    short? heartRateWarningMin = null,
    short? heartRateDangerMin = null,
    short? heartRateWarningMax = null,
    short? heartRateDangerMax = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdString, out var userId))
            {
                TempData["Error"] = "Unauthorized";
                return RedirectToAction(nameof(Dashboard));
            }

            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found";
                return RedirectToAction(nameof(Dashboard));
            }

            var alert = await _warningAlertService.GetAlertForResolveAsync(id);

            if (alert == null)
            {
                TempData["Error"] = "Cảnh báo không tồn tại.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (sendEmailInvitation)
            {
                if (!appointmentDate.HasValue)
                {
                    TempData["Error"] = "Vui lòng chọn ngày giờ hẹn tái khám khi gửi email.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                if (appointmentDate.Value <= DateTime.Now)
                {
                    TempData["Error"] = "Ngày hẹn tái khám phải là thời gian trong tương lai.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var thresholdResult =
                await _thresholdService.ValidateAndUpdateAsync(
                    alert,
                    doctor.Id,
                    systolicBpWarning,
                    systolicBpDanger,
                    diastolicBpWarning,
                    diastolicBpDanger,
                    heartRateWarningMin,
                    heartRateDangerMin,
                    heartRateWarningMax,
                    heartRateDangerMax);

            if (!thresholdResult.Success)
            {
                TempData["Error"] = thresholdResult.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            var resolveResult =
                await _warningAlertService.ResolveAlertAsync(
                    id,
                    doctor.Id,
                    resolutionNote);

            if (!resolveResult.Success)
            {
                TempData["Error"] = resolveResult.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            if (sendEmailInvitation)
            {
                var emailSent = await _emailTriggerService.SendAppointmentInvitationAsync(
                    id,
                    doctor.Id,
                    appointmentDate);

                if (!emailSent)
                {
                    TempData["Warning"] =
                        "Cảnh báo đã được xử lý thành công. Email mời tái khám chưa gửi được; vui lòng xem Lịch sử Email để biết nguyên nhân.";
                    return RedirectToAction(nameof(Dashboard));
                }

                TempData["Success"] =
                    "Đã xử lý cảnh báo và gửi email thành công.";

                return RedirectToAction(nameof(Dashboard));
            }

            TempData["Success"] =
                "Đã xử lý cảnh báo thành công.";

            return RedirectToAction(nameof(Dashboard));
        }
        [HttpGet]
        public async Task<IActionResult> Filter(byte? status, string? keyword, int page = 1, int pageSize = 10, bool onlyMyAlerts = false)
        {
            int? doctorId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                doctorId = doctor?.Id;
            }

            int? filterDoctorId = onlyMyAlerts ? doctorId : null;

            var totalRecords = await _warningAlertService.GetTotalAlertsAsync(status, keyword, filterDoctorId);
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var alerts = await _warningAlertService.GetAlertsAsync(
                status,
                keyword,
                page,
                pageSize,
                filterDoctorId);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Status = status;
            ViewBag.Keyword = keyword;

            ViewData["DoctorId"] = doctorId;

            return PartialView("_AlertTable", alerts);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var alert = await _warningAlertService.GetAlertDetailsAsync(id);

            if (alert == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy cảnh báo này.";
                return RedirectToAction(nameof(Dashboard));
            }

            int? doctorId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                doctorId = doctor?.Id;
            }
            ViewData["DoctorId"] = doctorId;

            var explanation = _explainabilityService.Explain(alert.Prediction, alert);
            ViewBag.Explanation = explanation;

            var clinicalHistory = await _warningAlertService.GetClinicalHistoryAsync(alert.PatientId);
            var dailyHistory = await _warningAlertService.GetDailyHistoryAsync(alert.PatientId);

            ViewBag.ClinicalHistory = clinicalHistory;
            ViewBag.DailyHistory = dailyHistory;

            return View(alert);
        }
    }
}

