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

        public async Task<IActionResult> Dashboard(byte? status,string? keyword,int page = 1)
        {
            int pageSize = 10;

            var alerts = await _warningAlertService
                .GetAlertsAsync(
                    status,
                    keyword,
                    page,
                    pageSize);

            var totalRecords =
                await _warningAlertService
                    .GetTotalAlertsAsync(
                        status,
                        keyword);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages =
                (int)Math.Ceiling(
                    (double)totalRecords / pageSize);

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;

            var warningalerts = await _warningAlertService.GetAlertsAsync(status,keyword,page,pageSize
                );

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
            var userIdString =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return Json(new
                {
                    success = false,
                    message = "Unauthorized"
                });
            }

            int userId = int.Parse(userIdString);

            var doctor = await _doctorService
                .GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Doctor not found"
                });
            }

            var success = await _warningAlertService
                .ClaimAlertAsync(id, doctor.Id);

            if (success)
            {
                return Json(new
                {
                    success = true,
                    message = "Đã tiếp nhận cảnh báo thành công."
                });
            }

            return Json(new
            {
                success = false,
                message = "Cảnh báo này đã được tiếp nhận bởi người khác."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(
    int id,
    string resolutionNote,
    bool sendEmailInvitation = false)
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

            int userId = int.Parse(userIdString);

            var doctor =
                await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Doctor not found"
                });
            }

            var success =
                await _warningAlertService.ResolveAlertAsync(
                    id,
                    doctor.Id,
                    resolutionNote);

            if (!success)
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn không có quyền xử lý cảnh báo này"
                });
            }

            if (sendEmailInvitation)
            {
                try
                {
                    var alert = await _context.WarningAlerts
                        .Include(x => x.Patient)
                        .ThenInclude(x => x.User)
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (alert != null &&
                        alert.Patient?.User != null)
                    {
                        string patientEmail =
                            alert.Patient.User.Email;

                        string patientName =
                            alert.Patient.User.FullName;

                        string doctorName =
                            doctor.User?.FullName
                            ?? "Smart Health Doctor";

                        var replacements =
                            new Dictionary<string, string>
                            {
                        { "{{PatientName}}", patientName },
                        { "{{AppointmentMessage}}", resolutionNote },
                        { "{{DoctorName}}", doctorName },
                        { "{{HospitalReplyContact}}",
                          "smarthealth.support@gmail.com | 1900-9999" }
                            };

                        string subject =
                            "Thư mời tái khám";

                        string body =
                            _emailService.GetHtmlContentFromFile(
                                "AppointmentInvitationTemplate.html",
                                replacements);

                        var emailNotification =
                            new EmailNotification
                            {
                                AlertId = alert.Id,
                                PatientId = alert.PatientId,
                                ToEmail = patientEmail,
                                Subject = subject,
                                Body = body,
                                Status = 0,
                                IsSent = false,
                                SentByDoctorId = doctor.Id,
                                CreatedAt = DateTime.Now
                            };

                        _context.EmailNotifications
                            .Add(emailNotification);

                        await _context.SaveChangesAsync();

                        await _emailService.SendEmailAsync(
                            patientEmail,
                            subject,
                            body);

                        emailNotification.Status = 1;
                        emailNotification.IsSent = true;
                        emailNotification.SentAt = DateTime.Now;

                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Gửi email lỗi: {ex.Message}"
                    });
                }
            }

            return Json(new
            {
                success = true,
                message = sendEmailInvitation
                    ? "Đã xử lý và gửi email thành công"
                    : "Đã xử lý cảnh báo thành công"
            });
        }
        [HttpGet]
        public async Task<IActionResult> Filter(byte? status,string? keyword,int page = 1)
        {
            int pageSize = 10;

            var alerts = await _warningAlertService
                .GetAlertsAsync(
                    status,
                    keyword,
                    page,
                    pageSize);

            var totalRecords = await _warningAlertService
                .GetTotalAlertsAsync(
                    status,
                    keyword);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages =
                (int)Math.Ceiling(
                    (double)totalRecords / pageSize);

            ViewBag.Status = status;
            ViewBag.Keyword = keyword;

            return PartialView(
                "_AlertTable",
                alerts);
        }
    }
}
