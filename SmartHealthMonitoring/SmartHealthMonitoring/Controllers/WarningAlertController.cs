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
    [Authorize(Roles = "Doctor")]

    public class WarningAlertController : Controller
    {
        private readonly IWarningAlertService _warningAlertService;
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;

        private readonly IDoctorService _doctorService;

        public WarningAlertController(
            IWarningAlertService warningAlertService,
            IDoctorService doctorService,
            SmartHealthMonitoringContext context,
            IEmailService emailService)
        {
            _warningAlertService = warningAlertService;
            _doctorService = doctorService;
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Dashboard(byte? status)
        {
            var alerts = await _warningAlertService.GetAlertsAsync(status);
            return View(alerts);
        }

        // ==================== RESOLVE ====================

        [HttpGet]
        public async Task<IActionResult> Resolve(int id)
        {
            var alert = await _context.WarningAlerts
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

            if (alert == null)
            {
                TempData["Error"] = "Không tìm thấy cảnh báo.";
                return RedirectToAction("Dashboard");
            }

            if (alert.Status == 2)
            {
                TempData["Error"] = "Cảnh báo này đã được xử lý rồi.";
                return RedirectToAction("Dashboard");
            }

            var model = new ResolveWarningViewModel
            {
                WarningAlertId = id,
                PatientName = alert.Patient?.User?.FullName ?? "Bệnh nhân"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(ResolveWarningViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var alert = await _context.WarningAlerts
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.Id == model.WarningAlertId && !a.IsDeleted);

            if (alert == null)
            {
                TempData["Error"] = "Không tìm thấy cảnh báo.";
                return RedirectToAction("Dashboard");
            }

            // Cập nhật trạng thái cảnh báo → Resolved
            alert.Status = 2;
            alert.ResolutionNote = model.ResolutionNote;
            await _context.SaveChangesAsync();

            // Nếu bác sĩ chọn gửi email mời tái khám
            if (model.SendEmailInvitation)
            {
                try
                {
                    var patient = alert.Patient;
                    var patientEmail = patient?.User?.Email;
                    var patientName = patient?.User?.FullName ?? "Bệnh nhân";

                    // Lấy thông tin bác sĩ đã claim cảnh báo (nếu có)
                    string doctorName = "Bác sĩ Smart Health";
                    if (alert.ClaimedByDoctorId.HasValue)
                    {
                        var doctor = await _context.Doctors
                            .Include(d => d.User)
                            .FirstOrDefaultAsync(d => d.Id == alert.ClaimedByDoctorId.Value);
                        doctorName = doctor?.User?.FullName ?? doctorName;
                    }

                    var replacements = new Dictionary<string, string>
                    {
                        { "{{PatientName}}", patientName },
                        { "{{AppointmentMessage}}", model.ResolutionNote },
                        { "{{DoctorName}}", doctorName },
                        { "{{HospitalReplyContact}}", "smarthealth.support@gmail.com | 1900-9999" }
                    };

                    string subject = "Thư Mời Tái Khám - Smart Health Monitoring";
                    string htmlBody = _emailService.GetHtmlContentFromFile("AppointmentInvitationTemplate.html", replacements);

                    // Tạo bản ghi EmailNotification với Status = 0 (Queued)
                    var notification = new EmailNotification
                    {
                        AlertId = alert.Id,
                        PatientId = alert.PatientId,
                        ToEmail = patientEmail ?? string.Empty,
                        Subject = subject,
                        Body = htmlBody,
                        Status = 0, // Queued
                        IsSent = false,
                        SentByDoctorId = alert.ClaimedByDoctorId,
                        CreatedAt = DateTime.Now
                    };
                    _context.EmailNotifications.Add(notification);
                    await _context.SaveChangesAsync();

                    // Thực thi gửi mail (try-catch riêng, không crash luồng chính)
                    if (!string.IsNullOrEmpty(patientEmail) && !string.IsNullOrEmpty(htmlBody))
                    {
                        await _emailService.SendEmailAsync(patientEmail, subject, htmlBody);
                        notification.Status = 1; // Sent
                        notification.IsSent = true;
                        notification.SentAt = DateTime.Now;
                    }
                    else
                    {
                        notification.Status = 2; // Failed
                        notification.ErrorMessage = "Email bệnh nhân bị rỗng hoặc template không tìm thấy.";
                    }
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EmailError] Gửi email mời tái khám thất bại: {ex.Message}");
                }
            }

            TempData["Success"] = "Đã xử lý cảnh báo thành công!" +
                (model.SendEmailInvitation ? " Thư mời tái khám đã được gửi cho bệnh nhân." : "");
            return RedirectToAction("Dashboard");
        }

        //Claim WarningAlert by Doctor

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim(int id)
        {
            // lấy user login
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

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
                TempData["Success"] = "Claim alert successfully";
            }
            else
            {
                TempData["Error"] = "Alert already claimed";
            }
            return RedirectToAction("Dashboard");
        }
    }
}
