using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")]
    public class WarningAlertController : Controller
    {
        private readonly IWarningAlertService _warningAlertService;
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;
        private readonly IDoctorService _doctorService;
        private readonly IEmailTriggerService _emailTriggerService;

        public WarningAlertController(
            IWarningAlertService warningAlertService,
            IDoctorService doctorService,
            SmartHealthMonitoringContext context,
            IEmailService emailService,
            IEmailTriggerService emailTriggerService)
        {
            _warningAlertService = warningAlertService;
            _doctorService = doctorService;
            _context = context;
            _emailService = emailService;
            _emailTriggerService = emailTriggerService;
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

            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int userId = int.Parse(userIdString);
            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found";
                return RedirectToAction("Dashboard");
            }

            var success = await _warningAlertService.ClaimAlertAsync(id, doctor.Id);

            if (success)
            {
                TempData["Success"] = "Đã tiếp nhận cảnh báo thành công.";
            }
            else
            {
                TempData["Error"] = "Cảnh báo này đã được tiếp nhận bởi người khác.";
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string resolutionNote, bool sendEmailInvitation = false, DateTime? appointmentDate = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString)) return Unauthorized();

            int userId = int.Parse(userIdString);
            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                TempData["Error"] = "Doctor not found";
                return RedirectToAction("Dashboard");
            }

            var success = await _warningAlertService.ResolveAlertAsync(id, doctor.Id, resolutionNote);

            if (success)
            {
                if (sendEmailInvitation)
                {
                    await _emailTriggerService.SendAppointmentInvitationAsync(id, doctor.Id, appointmentDate);
                    TempData["Success"] = "Đã xử lý & gửi email thư mời tái khám thành công!";
                }
                else
                {
                    TempData["Success"] = "Đã xử lý xong cảnh báo.";
                }
            }
            else
            {
                TempData["Error"] = "Bạn không có quyền xử lý cảnh báo này.";
            }

            return RedirectToAction("Dashboard");
        }
    }
}
