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
        private readonly EmailTemplateService _emailTemplateService;
        private readonly IQrCheckInService _qrCheckInService;

        public EmailTriggerService(
            SmartHealthMonitoringContext context,
            IEmailService emailService,
            EmailTemplateService emailTemplateService,
            IQrCheckInService qrCheckInService)
        {
            _context = context;
            _emailService = emailService;
            _emailTemplateService = emailTemplateService;
            _qrCheckInService = qrCheckInService;
        }

        public async Task<bool> SendAppointmentInvitationAsync(int alertId, int sentByDoctorId, DateTime? appointmentDate = null)
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
                    string htmlBody = _emailService.GetHtmlContentFromFile(templateName, replacements);

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

                    var emailSent = false;
                    if (!string.IsNullOrEmpty(htmlBody))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(patientEmail, subject, htmlBody);
                            notification.Status = 1;
                            notification.IsSent = true;
                            notification.SentAt = DateTime.Now;
                            emailSent = true;
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
                    return emailSent;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendAppointmentInvitationAsync Error] {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendHealthWarningAsync(int patientId, int predictionId)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .Include(p => p.EmergencyContacts)
                    .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);

                var prediction = await _context.AiriskPredictions
                    .FirstOrDefaultAsync(p => p.Id == predictionId && !p.IsDeleted);

                var alert = await _context.WarningAlerts
                    .FirstOrDefaultAsync(a => a.PredictionId == predictionId && !a.IsDeleted);

                if (patient == null || prediction == null || alert == null || prediction.RiskLevel < 2)
                {
                    return false;
                }

                var recipientEmails = new List<string>();
                
                // Tắt gửi mail cho bệnh nhân để tránh làm họ hoảng sợ theo yêu cầu
                // if (!string.IsNullOrWhiteSpace(patient.User?.Email))
                // {
                //     recipientEmails.Add(patient.User.Email.Trim());
                // }

                recipientEmails.AddRange(patient.EmergencyContacts
                    .Where(contact =>
                        contact.IsActive &&
                        !contact.IsDeleted &&
                        !string.IsNullOrWhiteSpace(contact.Email))
                    .Select(contact => contact.Email!.Trim()));

                recipientEmails = recipientEmails
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (recipientEmails.Count == 0)
                {
                    return false;
                }

                var patientName = patient.User?.FullName ?? "Bệnh nhân";
                var riskScorePercent = (prediction.RiskScore * 100m)
                    .ToString("F2", CultureInfo.InvariantCulture);

                var replacements = new Dictionary<string, string>
                {
                    { "{{PatientName}}", patientName },
                    { "{{RiskScore}}", riskScorePercent },
                    { "{{RiskLevel}}", prediction.RiskLevel.ToString() },
                    { "{{DetectedAt}}", prediction.PredictedAt.ToString("dd/MM/yyyy HH:mm:ss") }
                };

                const string templateName = "HealthWarningTemplate.html";
                var subject = _emailTemplateService.GetSubject(templateName, replacements);
                var htmlBody = _emailService.GetHtmlContentFromFile(templateName, replacements);

                var existingNotifications = await _context.EmailNotifications
                    .Where(notification =>
                        notification.AlertId == alert.Id &&
                        notification.PatientId == patientId &&
                        notification.SentByDoctorId == null)
                    .ToListAsync();

                var allSucceeded = true;

                foreach (var recipientEmail in recipientEmails)
                {
                    var notification = existingNotifications.FirstOrDefault(item =>
                        string.Equals(
                            item.ToEmail,
                            recipientEmail,
                            StringComparison.OrdinalIgnoreCase));

                    if (notification?.IsSent == true)
                    {
                        continue;
                    }

                    if (notification == null)
                    {
                        notification = new EmailNotification
                        {
                            AlertId = alert.Id,
                            PatientId = patientId,
                            ToEmail = recipientEmail,
                            SentByDoctorId = null,
                            CreatedAt = DateTime.Now
                        };
                        _context.EmailNotifications.Add(notification);
                        existingNotifications.Add(notification);
                    }

                    notification.ToEmail = recipientEmail;
                    notification.Subject = subject;
                    notification.Body = htmlBody;
                    notification.Status = 0;
                    notification.IsSent = false;
                    notification.SentAt = null;
                    notification.ErrorMessage = null;
                    await _context.SaveChangesAsync();

                    if (string.IsNullOrEmpty(htmlBody))
                    {
                        notification.Status = 2;
                        notification.ErrorMessage = "Template không tìm thấy.";
                        allSucceeded = false;
                        await _context.SaveChangesAsync();
                        continue;
                    }

                    var sendResult = await TrySendHealthWarningWithRetryAsync(
                        recipientEmail,
                        subject,
                        htmlBody);

                    if (sendResult.Success)
                    {
                        notification.Status = 1;
                        notification.IsSent = true;
                        notification.SentAt = DateTime.Now;
                    }
                    else
                    {
                        notification.Status = 2;
                        notification.IsSent = false;
                        notification.ErrorMessage = sendResult.ErrorMessage;
                        allSucceeded = false;
                    }

                    await _context.SaveChangesAsync();
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SendHealthWarningAsync Error] " + ex.Message);
                return false;
            }
        }

        private async Task<(bool Success, string? ErrorMessage)> TrySendHealthWarningWithRetryAsync(
            string recipientEmail,
            string subject,
            string htmlBody)
        {
            const int maxAttempts = 3;
            Exception? lastException = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await _emailService.SendEmailAsync(recipientEmail, subject, htmlBody);
                    return (true, null);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
                    }
                }
            }

            var errorMessage =
                (lastException?.Message ?? "Unknown SMTP error") +
                " (failed after 3 attempts).";
            return (false, errorMessage);
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
                    string htmlBody = _emailService.GetHtmlContentFromFile(templateName, replacements);

                    var notification = new EmailNotification
                    {
                        AlertId = null,
                        PatientId = patientId,
                        ToEmail = patientEmail,
                        Subject = subject,
                        Body = htmlBody,
                        Status = string.IsNullOrEmpty(htmlBody) ? (byte)2 : (byte)3,
                        IsSent = false,
                        SentByDoctorId = null,
                        CreatedAt = DateTime.Now,
                        ErrorMessage = string.IsNullOrEmpty(htmlBody) ? "Template không tìm thấy." : null
                    };
                    _context.EmailNotifications.Add(notification);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendDailyVitalLogReminderAsync Error] {ex.Message}");
            }
        }

        public async Task SendDoctorAcceptedCheckInAsync(int waitingId, int doctorId)
        {
            try
            {
                var waiting = await _context.WaitingPatients
                    .Include(w => w.Patient).ThenInclude(p => p.User)
                    .Include(w => w.Doctor).ThenInclude(d => d!.User)
                    .FirstOrDefaultAsync(w => w.Id == waitingId);

                if (waiting?.Patient?.User?.Email == null)
                    return;

                var doctor = waiting.Doctor
                    ?? await _context.Doctors
                        .Include(d => d.User)
                        .FirstOrDefaultAsync(d => d.Id == doctorId);

                if (doctor == null)
                    return;

                var acceptedAt = waiting.AcceptedAt ?? SmartHealthMonitoring.Common.AppTime.Now;
                var checkInCode = _qrCheckInService.BuildCheckInCode(
                    waiting.Id,
                    waiting.PatientId,
                    doctor.Id,
                    waiting.SequenceNumber,
                    acceptedAt);

                const string qrContentId = "qrcheckin";
                var qrPng = _qrCheckInService.GeneratePng(checkInCode);
                var qrDataUri = _qrCheckInService.GenerateDataUri(checkInCode);

                var patientEmail = waiting.Patient.User.Email;
                var patientName = waiting.Patient.User.FullName ?? "Bệnh nhân";
                var doctorName = doctor.User?.FullName ?? "Bác sĩ Smart Health";

                var replacements = new Dictionary<string, string>
                {
                    { "{{PatientName}}", patientName },
                    { "{{DoctorName}}", doctorName },
                    { "{{Specialty}}", doctor.Specialty ?? "Chưa cập nhật" },
                    { "{{RoomNumber}}", string.IsNullOrWhiteSpace(doctor.RoomNumber) ? "Chưa phân phòng" : doctor.RoomNumber },
                    { "{{SequenceNumber}}", waiting.SequenceNumber.ToString() },
                    { "{{AcceptedAt}}", acceptedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm") },
                    { "{{CheckInCode}}", checkInCode },
                    // Lưu body lịch sử email bằng data-URI; khi gửi thật dùng cid:
                    { "{{QrCodeImage}}", qrDataUri }
                };

                const string templateName = "DoctorAcceptedCheckInTemplate.html";
                var subject = _emailTemplateService.GetSubject(templateName, replacements);
                var htmlBodyForHistory = _emailService.GetHtmlContentFromFile(templateName, replacements);

                var notification = new EmailNotification
                {
                    AlertId = null,
                    PatientId = waiting.PatientId,
                    ToEmail = patientEmail,
                    Subject = subject,
                    Body = htmlBodyForHistory,
                    Status = 0,
                    IsSent = false,
                    SentByDoctorId = doctorId,
                    CreatedAt = DateTime.Now
                };
                _context.EmailNotifications.Add(notification);
                await _context.SaveChangesAsync();

                if (string.IsNullOrEmpty(htmlBodyForHistory))
                {
                    notification.Status = 2;
                    notification.IsSent = false;
                    notification.ErrorMessage = "Template không tìm thấy.";
                    await _context.SaveChangesAsync();
                    return;
                }

                try
                {
                    // Email client cần cid: để hiện QR inline
                    replacements["{{QrCodeImage}}"] = $"cid:{qrContentId}";
                    var htmlBodyToSend = _emailService.GetHtmlContentFromFile(templateName, replacements);

                    await _emailService.SendEmailAsync(
                        patientEmail,
                        subject,
                        htmlBodyToSend,
                        new Dictionary<string, byte[]> { { qrContentId, qrPng } });

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

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendDoctorAcceptedCheckInAsync Error] {ex.Message}");
            }
        }

        public async Task SendBookingConfirmationCheckInAsync(int appointmentId)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Slot)
                    .Include(a => a.Patient).ThenInclude(p => p.User)
                    .Include(a => a.Doctor).ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId);

                if (appointment?.Patient?.User?.Email == null || appointment.Slot == null || appointment.Doctor == null)
                    return;

                var checkInCode = _qrCheckInService.BuildAppointmentCheckInCode(
                    appointment.Id,
                    appointment.PatientId,
                    appointment.DoctorId,
                    appointment.Slot.SlotStart);

                const string qrContentId = "qrcheckin";
                var qrPng = _qrCheckInService.GeneratePng(checkInCode);
                var qrDataUri = _qrCheckInService.GenerateDataUri(checkInCode);

                var patientEmail = appointment.Patient.User.Email;
                var patientName = appointment.Patient.User.FullName ?? "Bệnh nhân";
                var doctorName = appointment.Doctor.User?.FullName ?? "Bác sĩ Smart Health";
                var appointmentTime =
                    $"{appointment.Slot.SlotStart:HH:mm} - {appointment.Slot.SlotEnd:HH:mm} (Ngày {appointment.Slot.SlotStart:dd/MM/yyyy})";

                var replacements = new Dictionary<string, string>
                {
                    { "{{PatientName}}", patientName },
                    { "{{DoctorName}}", doctorName },
                    { "{{Specialty}}", appointment.Doctor.Specialty ?? "Chưa cập nhật" },
                    { "{{RoomNumber}}", string.IsNullOrWhiteSpace(appointment.Doctor.RoomNumber) ? "Chưa phân phòng" : appointment.Doctor.RoomNumber },
                    { "{{AppointmentTime}}", appointmentTime },
                    { "{{AppointmentId}}", appointment.Id.ToString() },
                    { "{{CheckInCode}}", checkInCode },
                    { "{{QrCodeImage}}", qrDataUri }
                };

                const string templateName = "AppointmentBookingConfirmationTemplate.html";
                var subject = _emailTemplateService.GetSubject(templateName, replacements);
                var htmlBodyForHistory = _emailService.GetHtmlContentFromFile(templateName, replacements);

                var notification = new EmailNotification
                {
                    AlertId = null,
                    PatientId = appointment.PatientId,
                    ToEmail = patientEmail,
                    Subject = subject,
                    Body = htmlBodyForHistory,
                    Status = 0,
                    IsSent = false,
                    SentByDoctorId = appointment.DoctorId,
                    CreatedAt = DateTime.Now
                };
                _context.EmailNotifications.Add(notification);
                await _context.SaveChangesAsync();

                if (string.IsNullOrEmpty(htmlBodyForHistory))
                {
                    notification.Status = 2;
                    notification.IsSent = false;
                    notification.ErrorMessage = "Template không tìm thấy.";
                    await _context.SaveChangesAsync();
                    return;
                }

                try
                {
                    replacements["{{QrCodeImage}}"] = $"cid:{qrContentId}";
                    var htmlBodyToSend = _emailService.GetHtmlContentFromFile(templateName, replacements);

                    await _emailService.SendEmailAsync(
                        patientEmail,
                        subject,
                        htmlBodyToSend,
                        new Dictionary<string, byte[]> { { qrContentId, qrPng } });

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

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendBookingConfirmationCheckInAsync Error] {ex.Message}");
            }
        }

        public async Task SendAppointmentReminderAsync(int appointmentId, string reminderLabel)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Slot)
                    .Include(a => a.Patient).ThenInclude(p => p.User)
                    .Include(a => a.Doctor).ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId);

                if (appointment?.Patient?.User?.Email == null || appointment.Slot == null || appointment.Doctor == null)
                    return;

                var patientEmail = appointment.Patient.User.Email;
                var patientName = appointment.Patient.User.FullName ?? "Bệnh nhân";
                var doctorName = appointment.Doctor.User?.FullName ?? "Bác sĩ Smart Health";
                var appointmentTime =
                    $"{appointment.Slot.SlotStart:HH:mm} - {appointment.Slot.SlotEnd:HH:mm} (Ngày {appointment.Slot.SlotStart:dd/MM/yyyy})";

                var replacements = new Dictionary<string, string>
                {
                    { "{{PatientName}}", patientName },
                    { "{{DoctorName}}", doctorName },
                    { "{{Specialty}}", appointment.Doctor.Specialty ?? "Chưa cập nhật" },
                    { "{{RoomNumber}}", string.IsNullOrWhiteSpace(appointment.Doctor.RoomNumber) ? "Chưa phân phòng" : appointment.Doctor.RoomNumber },
                    { "{{AppointmentTime}}", appointmentTime },
                    { "{{AppointmentId}}", appointment.Id.ToString() },
                    { "{{ReminderLabel}}", reminderLabel }
                };

                const string templateName = "AppointmentReminderTemplate.html";
                var subject = _emailTemplateService.GetSubject(templateName, replacements);
                var htmlBody = _emailService.GetHtmlContentFromFile(templateName, replacements);

                var notification = new EmailNotification
                {
                    AlertId = null,
                    PatientId = appointment.PatientId,
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

                if (string.IsNullOrEmpty(htmlBody))
                {
                    notification.Status = 2;
                    notification.IsSent = false;
                    notification.ErrorMessage = "Template không tìm thấy.";
                    await _context.SaveChangesAsync();
                    return;
                }

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

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendAppointmentReminderAsync Error] {ex.Message}");
            }
        }
    }
}
