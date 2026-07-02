using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services;

public class SosNotificationService : ISosNotificationService
{
    private const string SosTemplateName = "SosEmergencyContactTemplate.html";
    private const string SosTemplateMarker = "EmailTemplate:SosEmergencyContactTemplate";

    private readonly SmartHealthMonitoringContext _context;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IAiAlertSettingsService _aiAlertSettingsService;
    private readonly ILogger<SosNotificationService> _logger;

    public SosNotificationService(
        SmartHealthMonitoringContext context,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IAiAlertSettingsService aiAlertSettingsService,
        ILogger<SosNotificationService> logger)
    {
        _context = context;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _aiAlertSettingsService = aiAlertSettingsService;
        _logger = logger;
    }

    public async Task NotifyEmergencyContactsAsync(int alertId, CancellationToken cancellationToken = default)
    {
        var alert = await _context.WarningAlerts
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Patient).ThenInclude(p => p.EmergencyContacts)
            .Include(a => a.Prediction)
            .FirstOrDefaultAsync(a => a.Id == alertId && !a.IsDeleted, cancellationToken);

        if (alert == null)
        {
            _logger.LogWarning("[SOS] Alert #{AlertId} not found.", alertId);
            return;
        }

        if (!_aiAlertSettingsService.IsHighPriority(alert.Prediction))
        {
            _logger.LogInformation(
                "[SOS] Skip alert #{AlertId}. Dashboard priority is not high. RiskLevel={RiskLevel}, RiskScore={RiskScore:F4}.",
                alert.Id,
                alert.Prediction.RiskLevel,
                alert.Prediction.RiskScore);
            return;
        }

        var contacts = alert.Patient.EmergencyContacts
            .Where(c => c.IsActive && !c.IsDeleted && !string.IsNullOrWhiteSpace(c.Email))
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FullName)
            .ToList();

        if (!contacts.Any())
        {
            _logger.LogDebug("[SOS] Patient #{PatientId} has no active emergency contact email.", alert.PatientId);
            return;
        }

        var patientName = alert.Patient.User?.FullName ?? "Bệnh nhân";
        var detectedAt = alert.Prediction.PredictedAt.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture);
        var riskPercent = (alert.Prediction.RiskScore * 100m).ToString("F1", CultureInfo.InvariantCulture);

        foreach (var contact in contacts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await SendEmailAsync(alert, contact, patientName, detectedAt, riskPercent);
        }
    }

    private async Task SendEmailAsync(
        WarningAlert alert,
        EmergencyContact contact,
        string patientName,
        string detectedAt,
        string riskPercent)
    {
        var replacements = new Dictionary<string, string>
        {
            ["{{ContactName}}"] = contact.FullName,
            ["{{PatientName}}"] = patientName,
            ["{{DetectedAt}}"] = detectedAt,
            ["{{RiskScore}}"] = riskPercent,
            ["{{RiskLevel}}"] = alert.Prediction.RiskLevel.ToString(CultureInfo.InvariantCulture)
        };

        var fallbackSubject = $"[SOS] Cảnh báo khẩn cấp cho {patientName}";
        var subject = _emailTemplateService.GetSubject(SosTemplateName, replacements);
        if (string.IsNullOrWhiteSpace(subject))
        {
            subject = fallbackSubject;
        }

        var legacySubject = $"[SOS] Canh bao khan cap cho {patientName}";

        var alreadySent = await _context.EmailNotifications.AnyAsync(n =>
            n.AlertId == alert.Id &&
            n.PatientId == alert.PatientId &&
            n.ToEmail == contact.Email &&
            (n.Subject == subject ||
             n.Subject == fallbackSubject ||
             n.Subject == legacySubject ||
             n.Subject.StartsWith("[SOS]") ||
             n.Body.Contains(SosTemplateMarker)) &&
            n.IsSent);

        if (alreadySent)
        {
            _logger.LogInformation("[SOS EMAIL] Already sent to {Email} for alert #{AlertId}.", contact.Email, alert.Id);
            return;
        }

        var body = _emailTemplateService.RenderBody(SosTemplateName, replacements);
        if (string.IsNullOrWhiteSpace(body))
        {
            body = BuildFallbackEmailBody(contact.FullName, patientName, detectedAt, riskPercent, alert.Prediction.RiskLevel);
        }

        var notification = new EmailNotification
        {
            AlertId = alert.Id,
            PatientId = alert.PatientId,
            ToEmail = contact.Email!,
            Subject = subject,
            Body = body,
            Status = 0,
            IsSent = false,
            SentByDoctorId = null,
            CreatedAt = DateTime.Now
        };

        _context.EmailNotifications.Add(notification);
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendEmailAsync(contact.Email!, subject, body);
            notification.Status = 1;
            notification.IsSent = true;
            notification.SentAt = DateTime.Now;

            _logger.LogWarning(
                "[SOS EMAIL] Sent SOS email to {Email} for patient #{PatientId}, alert #{AlertId}.",
                contact.Email,
                alert.PatientId,
                alert.Id);
        }
        catch (Exception ex)
        {
            notification.Status = 2;
            notification.IsSent = false;
            notification.ErrorMessage = ex.Message;

            _logger.LogError(ex, "[SOS EMAIL] Failed to send SOS email to {Email}.", contact.Email);
        }

        await _context.SaveChangesAsync();
    }

    private static string BuildFallbackEmailBody(
        string contactName,
        string patientName,
        string detectedAt,
        string riskPercent,
        byte riskLevel)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="utf-8">
              <meta name="email-template" content="EmailTemplate:SosEmergencyContactTemplate">
            </head>
            <body style="margin:0;background:#f8fafc;font-family:Arial,sans-serif;color:#0f172a;">
              <div style="max-width:640px;margin:0 auto;padding:24px;">
                <div style="background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #fee2e2;box-shadow:0 18px 38px rgba(15,23,42,.08);">
                  <div style="background:#dc2626;color:#ffffff;padding:28px;text-align:center;">
                    <div style="font-size:42px;line-height:1;">SOS</div>
                    <h1 style="margin:10px 0 0;font-size:24px;">Cảnh báo sức khỏe khẩn cấp</h1>
                    <p style="margin:8px 0 0;color:#fee2e2;">Smart Health Monitoring</p>
                  </div>
                  <div style="padding:28px;">
                    <p>Xin chào <strong>{contactName}</strong>,</p>
                    <p>
                      Hệ thống AI vừa phát hiện <strong>{patientName}</strong> có điểm rủi ro đạt ngưỡng SOS.
                      Email này được gửi tự động tới người thân đã khai báo trong hệ thống.
                    </p>
                    <div style="background:#fff1f2;border-left:5px solid #dc2626;border-radius:10px;padding:16px;margin:20px 0;">
                      <p style="margin:0 0 8px;"><strong>Thời điểm phát hiện:</strong> {detectedAt}</p>
                      <p style="margin:0 0 8px;"><strong>Điểm rủi ro:</strong> {riskPercent}%</p>
                      <p style="margin:0;"><strong>Mức cảnh báo:</strong> {riskLevel}</p>
                    </div>
                    <p style="font-weight:700;color:#b91c1c;">
                      Vui lòng liên hệ bệnh nhân ngay. Nếu bệnh nhân đau ngực, khó thở, ngất, yếu liệt,
                      vã mồ hôi lạnh hoặc có dấu hiệu bất thường nghiêm trọng, hãy gọi cấp cứu 115
                      hoặc đưa tới cơ sở y tế gần nhất.
                    </p>
                    <p style="font-size:13px;color:#64748b;margin-top:24px;">
                      Đây là email tự động từ hệ thống SmartHealth. Email không thay thế chẩn đoán y khoa trực tiếp.
                    </p>
                  </div>
                </div>
              </div>
            </body>
            </html>
            """;
    }
}
