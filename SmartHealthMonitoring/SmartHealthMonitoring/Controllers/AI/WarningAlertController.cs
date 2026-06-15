using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.Services.AI;
using SmartHealthMonitoring.ViewModels;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers.AI
{
    [Authorize(Roles = "1")]
    public class WarningAlertController : Controller
    {
        private readonly IAiWarningAlertService _warningAlertService;
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;
        private readonly IDoctorService _doctorService;
        private readonly IEmailTriggerService _emailTriggerService;
        private readonly IAuditLogService _auditLogService;

        public WarningAlertController(
            IAiWarningAlertService warningAlertService,
            IDoctorService doctorService,
            SmartHealthMonitoringContext context,
            IEmailService emailService,
            IEmailTriggerService emailTriggerService,
            IAuditLogService auditLogService)
        {
            _warningAlertService = warningAlertService;
            _doctorService = doctorService;
            _context = context;
            _emailService = emailService;
            _emailTriggerService = emailTriggerService;
            _auditLogService = auditLogService;
        }

        public async Task<IActionResult> Dashboard(byte? status, string? keyword, int page = 1, int pageSize = 10)
        {
            var totalRecords = await _warningAlertService.GetTotalAlertsAsync(status, keyword);
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var alerts = await _warningAlertService.GetAlertsAsync(
                status,
                keyword,
                page,
                pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.TotalRecords = totalRecords;

            // Truyền doctorId để UI chỉ hiển thị Resolution note cho đúng bác sĩ đã claim
            int? doctorId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                doctorId = doctor?.Id;
            }

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
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            int userId = int.Parse(userIdString);
            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                return Json(new { success = false, message = "Doctor not found" });
            }

            var success = await _warningAlertService.ClaimAlertAsync(id, doctor.Id);

            if (success)
            {
                var alert = await _context.WarningAlerts
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.Prediction)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);

                await _auditLogService.LogAsync(
                    "Claim",
                    "WarningAlert",
                    id.ToString(),
                    $"Tiếp nhận cảnh báo AI #{id} của bệnh nhân {alert?.Patient?.User?.FullName ?? "không xác định"}; mức rủi ro {alert?.Prediction?.RiskLevel.ToString() ?? "không xác định"}.",
                    alert?.Patient?.UserId,
                    alert?.Patient?.User?.FullName);

                return Json(new { success = true, message = "Đã tiếp nhận cảnh báo thành công." });
            }

            return Json(new { success = false, message = "Cảnh báo này đã được tiếp nhận bởi người khác." });
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

            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            int userId = int.Parse(userIdString);
            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                return Json(new { success = false, message = "Doctor not found" });
            }

            // Fetch alert with Patient and PatientThreshold details first to validate and perform threshold updates
            var alert = await _context.WarningAlerts
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.PatientThreshold)
                .Include(a => a.Prediction)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alert == null)
            {
                return Json(new { success = false, message = "Cảnh báo không tồn tại." });
            }

            // Perform Range Validation if any threshold values are provided
            bool hasThresholdUpdate = systolicBpWarning.HasValue ||
                                      systolicBpDanger.HasValue ||
                                      diastolicBpWarning.HasValue ||
                                      diastolicBpDanger.HasValue ||
                                      heartRateWarningMin.HasValue ||
                                      heartRateDangerMin.HasValue ||
                                      heartRateWarningMax.HasValue ||
                                      heartRateDangerMax.HasValue;

            if (hasThresholdUpdate)
            {
                // Current or default values for range validation
                var currentThreshold = alert.Patient.PatientThreshold;
                short sysWarn = systolicBpWarning ?? (currentThreshold?.SystolicBpWarning ?? 130);
                short sysDanger = systolicBpDanger ?? (currentThreshold?.SystolicBpDanger ?? 140);
                if (sysWarn >= sysDanger)
                {
                    return Json(new { success = false, message = "Ngưỡng Cảnh báo Huyết áp TT phải nhỏ hơn ngưỡng Nguy hiểm." });
                }

                short diaWarn = diastolicBpWarning ?? (currentThreshold?.DiastolicBpWarning ?? 80);
                short diaDanger = diastolicBpDanger ?? (currentThreshold?.DiastolicBpDanger ?? 90);
                if (diaWarn >= diaDanger)
                {
                    return Json(new { success = false, message = "Ngưỡng Cảnh báo Huyết áp TR phải nhỏ hơn ngưỡng Nguy hiểm." });
                }

                short hrWarnMin = heartRateWarningMin ?? (currentThreshold?.HeartRateWarningMin ?? 60);
                short hrDangerMin = heartRateDangerMin ?? (currentThreshold?.HeartRateDangerMin ?? 50);
                if (hrDangerMin >= hrWarnMin)
                {
                    return Json(new { success = false, message = "Ngưỡng Nguy hiểm Nhịp tim thấp phải nhỏ hơn ngưỡng Cảnh báo." });
                }

                short hrWarnMax = heartRateWarningMax ?? (currentThreshold?.HeartRateWarningMax ?? 100);
                short hrDangerMax = heartRateDangerMax ?? (currentThreshold?.HeartRateDangerMax ?? 120);
                if (hrWarnMax >= hrDangerMax)
                {
                    return Json(new { success = false, message = "Ngưỡng Cảnh báo Nhịp tim cao phải nhỏ hơn ngưỡng Nguy hiểm." });
                }
            }

            var success = await _warningAlertService.ResolveAlertAsync(id, doctor.Id, resolutionNote);

            if (success)
            {
                if (hasThresholdUpdate)
                {
                    var threshold = alert.Patient.PatientThreshold;
                    bool isNew = (threshold == null);
                    if (isNew)
                    {
                        threshold = new PatientThreshold
                        {
                            PatientId = alert.PatientId
                        };
                    }

                    threshold.SystolicBpWarning = systolicBpWarning ?? threshold.SystolicBpWarning;
                    threshold.SystolicBpDanger = systolicBpDanger ?? threshold.SystolicBpDanger;
                    threshold.DiastolicBpWarning = diastolicBpWarning ?? threshold.DiastolicBpWarning;
                    threshold.DiastolicBpDanger = diastolicBpDanger ?? threshold.DiastolicBpDanger;
                    threshold.HeartRateWarningMin = heartRateWarningMin ?? threshold.HeartRateWarningMin;
                    threshold.HeartRateDangerMin = heartRateDangerMin ?? threshold.HeartRateDangerMin;
                    threshold.HeartRateWarningMax = heartRateWarningMax ?? threshold.HeartRateWarningMax;
                    threshold.HeartRateDangerMax = heartRateDangerMax ?? threshold.HeartRateDangerMax;
                    threshold.UpdatedAt = DateTime.Now;
                    threshold.UpdatedByDoctorId = doctor.Id;

                    if (isNew)
                    {
                        _context.PatientThresholds.Add(threshold);
                    }
                    await _context.SaveChangesAsync();

                    // Log Audit for PatientThreshold update
                    await _auditLogService.LogAsync(
                        isNew ? "Create" : "Update",
                        "PatientThreshold",
                        threshold.Id.ToString(),
                        $"{(isNew ? "Tạo" : "Cập nhật")} ngưỡng riêng cho bệnh nhân {alert.Patient?.User?.FullName ?? $"#{alert.PatientId}"} khi xử lý cảnh báo; huyết áp tâm thu {threshold.SystolicBpWarning}/{threshold.SystolicBpDanger}, nhịp tim {threshold.HeartRateWarningMin}-{threshold.HeartRateWarningMax}.",
                        alert.Patient?.UserId,
                        alert.Patient?.User?.FullName);
                }

                // Log Audit for WarningAlert resolution
                await _auditLogService.LogAsync(
                    "Resolve",
                    "WarningAlert",
                    id.ToString(),
                    $"Xử lý cảnh báo AI #{id} của bệnh nhân {alert.Patient?.User?.FullName ?? "không xác định"}; mức rủi ro {alert.Prediction?.RiskLevel.ToString() ?? "không xác định"}.",
                    alert.Patient?.UserId,
                    alert.Patient?.User?.FullName);

                if (sendEmailInvitation)
                {
                    await _emailTriggerService.SendAppointmentInvitationAsync(id, doctor.Id, appointmentDate);
                    return Json(new { success = true, message = "Đã xử lý và gửi email thành công" });
                }
                else
                {
                    return Json(new { success = true, message = "Đã xử lý cảnh báo thành công" });
                }
            }

            return Json(new { success = false, message = "Bạn không có quyền xử lý cảnh báo này." });
        }

        [HttpGet]
        public async Task<IActionResult> Filter(byte? status, string? keyword, int page = 1, int pageSize = 10)
        {
            var totalRecords = await _warningAlertService.GetTotalAlertsAsync(status, keyword);
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            page = Math.Max(1, Math.Min(page, totalPages));

            var alerts = await _warningAlertService.GetAlertsAsync(
                status,
                keyword,
                page,
                pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Status = status;
            ViewBag.Keyword = keyword;

            // Cũng cần truyền ViewData["DoctorId"] cho _AlertTable
            int? doctorId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                doctorId = doctor?.Id;
            }
            ViewData["DoctorId"] = doctorId;

            return PartialView("_AlertTable", alerts);
        }
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var model = await _warningAlertService
                .GetDetailAsync(id);

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }
    }
}
