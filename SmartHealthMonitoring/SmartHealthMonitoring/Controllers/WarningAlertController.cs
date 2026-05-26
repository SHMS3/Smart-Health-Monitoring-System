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

            // Truyền doctorId để UI chỉ hiển thị Resolution note cho đúng bác sĩ đã claim
            int? doctorId = null;
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out var userId))
            {
                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                doctorId = doctor?.Id;
            }

            ViewData["DoctorId"] = doctorId;

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
        public async Task<IActionResult> Resolve(int id, string resolutionNote, bool sendEmailInvitation = false)
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
                // Thêm tính năng GỬI EMAIL của nhánh cũ vào đây!
                if (sendEmailInvitation)
                {
                    try
                    {
                        var alert = await _context.WarningAlerts
                            .Include(a => a.Patient).ThenInclude(p => p.User)
                            .FirstOrDefaultAsync(a => a.Id == id);

                        if (alert != null && alert.Patient?.User?.Email != null)
                        {
                            var patientEmail = alert.Patient.User.Email;
                            var patientName = alert.Patient.User.FullName ?? "Bệnh nhân";
                            string doctorName = doctor.User?.FullName ?? "Bác sĩ Smart Health";

                            var replacements = new Dictionary<string, string>
                            {
                                { "{{PatientName}}", patientName },
                                { "{{AppointmentMessage}}", resolutionNote },
                                { "{{DoctorName}}", doctorName },
                                { "{{HospitalReplyContact}}", "smarthealth.support@gmail.com | 1900-9999" }
                            };

                            string subject = "Thư Mời Tái Khám - Smart Health Monitoring";
                            string htmlBody = _emailService.GetHtmlContentFromFile("AppointmentInvitationTemplate.html", replacements);

                            var notification = new EmailNotification
                            {
                                AlertId = alert.Id,
                                PatientId = alert.PatientId,
                                ToEmail = patientEmail,
                                Subject = subject,
                                Body = htmlBody,
                                Status = 0,
                                IsSent = false,
                                SentByDoctorId = doctor.Id,
                                CreatedAt = DateTime.Now
                            };
                            _context.EmailNotifications.Add(notification);
                            await _context.SaveChangesAsync();

                            if (!string.IsNullOrEmpty(htmlBody))
                            {
                                await _emailService.SendEmailAsync(patientEmail, subject, htmlBody);
                                notification.Status = 1;
                                notification.IsSent = true;
                                notification.SentAt = DateTime.Now;
                            }
                            else
                            {
                                notification.Status = 2;
                                notification.ErrorMessage = "Template không tìm thấy.";
                            }
                            await _context.SaveChangesAsync();
                            
                            TempData["Success"] = "Đã xử lý & gửi email thư mời tái khám thành công!";
                            return RedirectToAction("Dashboard");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EmailError] {ex.Message}");
                    }
                }
                
                TempData["Success"] = "Đã xử lý xong cảnh báo.";
            }
            else
            {
                TempData["Error"] = "Bạn không có quyền xử lý cảnh báo này.";
            }

            return RedirectToAction("Dashboard");
        }
    }
}
