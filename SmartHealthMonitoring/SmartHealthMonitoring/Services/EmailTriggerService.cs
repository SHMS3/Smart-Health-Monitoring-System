using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public class EmailTriggerService : IEmailTriggerService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _emailTemplateService;

        public EmailTriggerService(
            SmartHealthMonitoringContext context,
            IEmailService emailService,
            IEmailTemplateService emailTemplateService)
        {
            _context = context;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
        }

        public async Task SendAppointmentInvitationAsync(int alertId, int sentByDoctorId, DateTime? appointmentDate = null)
        {
            try
            {
                var alert = await _context.WarningAlerts
                    .Include(a => a.Patient).ThenInclude(p => p.User)
                    .Include(a => a.Prediction).ThenInclude(p => p.ClinicalRecord)
                    .FirstOrDefaultAsync(a => a.Id == alertId);

                if (alert != null && alert.Patient?.User?.Email != null)
                {
                    var doctor = await _context.Doctors
                        .Include(d => d.User)
                        .FirstOrDefaultAsync(d => d.Id == sentByDoctorId);

                    var patientEmail = alert.Patient.User.Email;
                    var patientName = alert.Patient.User.FullName ?? "Bệnh nhân";
                    string doctorName = doctor?.User?.FullName ?? "Bác sĩ Smart Health";

                    string lastVisitDateDisplay = alert.Prediction?.ClinicalRecord?.VisitDate.ToString("dd/MM/yyyy") ?? alert.Prediction?.PredictedAt.ToString("dd/MM/yyyy HH:mm") ?? "Chưa ghi nhận";

                    var replacements = new Dictionary<string, string>
                    {
                        { "{{PatientName}}", patientName },
                        { "{{AppointmentMessage}}", alert.ResolutionNote ?? string.Empty },
                        { "{{DoctorName}}", doctorName },
                        { "{{HospitalReplyContact}}", "smarthealth.support@gmail.com | 1900-9999" },
                        { "{{LastExamDate}}", lastVisitDateDisplay },
                        { "{{AppointmentDate}}", appointmentDate.HasValue ? appointmentDate.Value.ToString("dd/MM/yyyy HH:mm") : "Sắp xếp cùng bác sĩ" }
                    };

                    string subject = "Thư Mời Tái Khám - Smart Health Monitoring";
                    const string templateName = "AppointmentInvitationTemplate.html";
                    subject = _emailTemplateService.GetSubject(templateName, replacements);
                    string htmlBody = _emailTemplateService.RenderBody(templateName, replacements);

                    var notification = new EmailNotification
                    {
                        AlertId = alert.Id,
                        PatientId = alert.PatientId,
                        ToEmail = patientEmail,
                        Subject = subject,
                        Body = htmlBody,
                        Status = 0,
                        IsSent = false,
                        SentByDoctorId = sentByDoctorId,
                        CreatedAt = DateTime.Now
                    };
                    _context.EmailNotifications.Add(notification);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(htmlBody))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(patientEmail, subject, htmlBody);
                            notification.Status = 1;
                            notification.IsSent = true;
                            notification.SentAt = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            notification.Status = 2;
                            notification.IsSent = false;
                            notification.ErrorMessage = ex.Message;
                            Console.WriteLine($"[EmailError] {ex.Message}");
                        }
                    }
                    else
                    {
                        notification.Status = 2;
                        notification.IsSent = false;
                        notification.ErrorMessage = "Template không tìm thấy.";
                    }
                    
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendAppointmentInvitationAsync Error] {ex.Message}");
            }
        }

        public async Task SendHealthWarningAsync(int patientId, int predictionId)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                var prediction = await _context.AiriskPredictions
                    .FirstOrDefaultAsync(p => p.Id == predictionId);

                if (patient?.User?.Email != null && prediction != null)
                {
                    // Lấy WarningAlert để gán AlertId (Foreign Key bắt buộc trong EmailNotification)
                    var alert = await _context.WarningAlerts.FirstOrDefaultAsync(a => a.PredictionId == predictionId);
                    if (alert == null || prediction.RiskScore <= 0.70m)
                    {
                        return;
                    }

                    var alreadySent = await _context.EmailNotifications.AnyAsync(n =>
                        n.AlertId == alert.Id &&
                        n.PatientId == patientId &&
                        n.SentByDoctorId == null);

                    if (alreadySent)
                    {
                        return;
                    }

                    var patientEmail = patient.User.Email;
                    var patientName = patient.User.FullName ?? "Bệnh nhân";
                    var riskScorePercent = (prediction.RiskScore * 100m)
                        .ToString("F2", CultureInfo.InvariantCulture);

                    var replacements = new Dictionary<string, string>
                    {
                        { "{{PatientName}}", patientName },
                        { "{{RiskScore}}", riskScorePercent },
                        { "{{RiskLevel}}", prediction.RiskLevel.ToString() },
                        { "{{DetectedAt}}", prediction.PredictedAt.ToString("dd/MM/yyyy HH:mm:ss") }
                    };

                    string subject = "CẢNH BÁO SỨC KHỎE KHẨN CẤP - Cần tới khám ngay";
                    const string templateName = "HealthWarningTemplate.html";
                    subject = _emailTemplateService.GetSubject(templateName, replacements);
                    string htmlBody = _emailTemplateService.RenderBody(templateName, replacements);

                    var notification = new EmailNotification
                    {
                        AlertId = alert.Id,
                        PatientId = patientId,
                        ToEmail = patientEmail,
                        Subject = subject,
                        Body = htmlBody,
                        Status = 0,
                        IsSent = false,
                        SentByDoctorId = null, // Hệ thống tự động gửi
                        CreatedAt = DateTime.Now
                    };
                    _context.EmailNotifications.Add(notification);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(htmlBody))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(patientEmail, subject, htmlBody);
                            notification.Status = 1;
                            notification.IsSent = true;
                            notification.SentAt = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            notification.Status = 2;
                            notification.IsSent = false;
                            notification.ErrorMessage = ex.Message;
                            Console.WriteLine($"[EmailError] {ex.Message}");
                        }
                    }
                    else
                    {
                        notification.Status = 2;
                        notification.IsSent = false;
                        notification.ErrorMessage = "Template không tìm thấy.";
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendHealthWarningAsync Error] {ex.Message}");
            }
        }

        public async Task SendDailyVitalLogReminderAsync(int patientId, string lastLogTimeDisplay)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient?.User?.Email != null)
                {
                    var patientEmail = patient.User.Email;
                    var patientName = patient.User.FullName ?? "Bệnh nhân";

                    var replacements = new Dictionary<string, string>
                    {
                        { "{{PatientName}}", patientName },
                        { "{{LastLogTimeDisplay}}", lastLogTimeDisplay },
                        { "{{ActionUrl}}", "http://localhost:5033/Patient/Create" }
                    };

                    string subject = "NHẮC NHỞ: Vui lòng ghi nhận chỉ số sức khỏe hàng ngày - Smart Health";
                    const string templateName = "VitalLogReminderTemplate.html";
                    subject = _emailTemplateService.GetSubject(templateName, replacements);
                    string htmlBody = _emailTemplateService.RenderBody(templateName, replacements);

                    var notification = new EmailNotification
                    {
                        AlertId = null,
                        PatientId = patientId,
                        ToEmail = patientEmail,
                        Subject = subject,
                        Body = htmlBody,
                        Status = 0,
                        IsSent = false,
                        SentByDoctorId = null,
                        CreatedAt = DateTime.Now
                    };
                    _context.EmailNotifications.Add(notification);
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(htmlBody))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(patientEmail, subject, htmlBody);
                            notification.Status = 1;
                            notification.IsSent = true;
                            notification.SentAt = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            notification.Status = 2;
                            notification.IsSent = false;
                            notification.ErrorMessage = ex.Message;
                            Console.WriteLine($"[EmailError] {ex.Message}");
                        }
                    }
                    else
                    {
                        notification.Status = 2;
                        notification.IsSent = false;
                        notification.ErrorMessage = "Template không tìm thấy.";
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendDailyVitalLogReminderAsync Error] {ex.Message}");
            }
        }
    }
}
