using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services;

public class EmailTemplateService
{
    private const string SubjectConfigFileName = "template-subjects.json";
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailTemplateService> _logger;
    private static readonly Regex TokenRegex = new(@"\{\{[^{}]+\}\}", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyList<EmailTemplateDefinition> Definitions = new List<EmailTemplateDefinition>
    {
        new()
        {
            TemplateName = "AppointmentInvitationTemplate.html",
            DisplayName = "Mời tái khám",
            Description = "Gửi cho bệnh nhân sau khi bác sĩ xử lý cảnh báo và cần hẹn tái khám.",
            DefaultSubject = "Thư Mời Tái Khám - Smart Health Monitoring",
        },
        new()
        {
            TemplateName = "HealthWarningTemplate.html",
            DisplayName = "Cảnh báo sức khỏe",
            Description = "Gửi tự động khi AI phát hiện nguy cơ sức khỏe cao.",
            DefaultSubject = "CẢNH BÁO SỨC KHỎE KHẨN CẤP - Cần tới khám ngay",
        },
        new()
        {
            TemplateName = "VitalLogReminderTemplate.html",
            DisplayName = "Nhắc ghi chỉ số",
            Description = "Nhắc bệnh nhân cập nhật chỉ số sinh hiệu hằng ngày.",
            DefaultSubject = "NHẮC NHỞ: Vui lòng ghi nhận chỉ số sức khỏe hằng ngày - Smart Health",
        },
        new()
        {
            TemplateName = "PatientHealthReportTemplate.html",
            DisplayName = "Báo cáo y tế",
            Description = "Mẫu báo cáo tình trạng y tế gửi cho bệnh nhân khi cần.",
            DefaultSubject = "Báo cáo Tình trạng Y tế - Smart Health",
        },
        new()
        {
            TemplateName = "WarningAlertTemplate.html",
            DisplayName = "Cảnh báo chỉ số",
            Description = "Mẫu cảnh báo chỉ số sức khỏe dùng cho các luồng cảnh báo cũ.",
            DefaultSubject = "CẢNH BÁO: Chỉ số sức khỏe bất thường - Smart Health",
        },
        new()
        {
            TemplateName = "DoctorAcceptedCheckInTemplate.html",
            DisplayName = "QR Check-in khi bác sĩ tiếp nhận",
            Description = "Gửi cho bệnh nhân ngay khi bác sĩ tiếp nhận thành công trong hàng đợi khám.",
            DefaultSubject = "Bác sĩ đã tiếp nhận - QR Check-in của bạn - Smart Health",
        },
        new()
        {
            TemplateName = "AppointmentBookingConfirmationTemplate.html",
            DisplayName = "Xác nhận đặt lịch + QR Check-in",
            Description = "NTF-01: Gửi khi lễ tân duyệt đặt lịch (BOOK-08) thành công.",
            DefaultSubject = "Xác nhận đặt lịch thành công - QR Check-in - Smart Health",
        },
        new()
        {
            TemplateName = "AppointmentReminderTemplate.html",
            DisplayName = "Nhắc lịch khám 24h/2h",
            Description = "NTF-02: Email nhắc trước giờ khám 24 giờ hoặc 2 giờ.",
            DefaultSubject = "Nhắc lịch khám - còn {{ReminderLabel}} - Smart Health",
        }
    };

    public EmailTemplateService(IWebHostEnvironment env, ILogger<EmailTemplateService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public IReadOnlyList<EmailTemplateListItemViewModel> GetTemplateList()
    {
        var subjects = ReadSubjects();

        return Definitions
            .Select(definition =>
            {
                var fileInfo = GetTemplateFileInfo(definition.TemplateName);
                return new EmailTemplateListItemViewModel
                {
                    TemplateName = definition.TemplateName,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    Subject = GetSubject(definition, subjects),
                    LastModifiedAt = fileInfo.Exists ? fileInfo.LastWriteTime : null,
                    FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                };
            })
            .ToList();
    }

    public async Task<EmailTemplateEditViewModel?> GetTemplateForEditAsync(string templateName)
    {
        var definition = FindDefinition(templateName);
        if (definition == null)
        {
            return null;
        }

        var fileInfo = GetTemplateFileInfo(definition.TemplateName);
        if (!fileInfo.Exists)
        {
            return null;
        }

        var subjects = ReadSubjects();
        var htmlContent = await File.ReadAllTextAsync(fileInfo.FullName, Encoding.UTF8);
        var subject = GetSubject(definition, subjects);
        var tokens = TokenRegex.Matches(subject + htmlContent)
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new EmailTemplateEditViewModel
        {
            TemplateName = definition.TemplateName,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Subject = subject,
            HtmlContent = htmlContent,
            Tokens = tokens,
            LastModifiedAt = fileInfo.LastWriteTime,
        };
    }

    public async Task<ServiceResult> UpdateTemplateAsync(EmailTemplateEditViewModel model)
    {
        var definition = FindDefinition(model.TemplateName);
        if (definition == null)
        {
            return ServiceResult.Fail("Template không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            return ServiceResult.Fail("Vui lòng nhập tiêu đề email.");
        }

        if (string.IsNullOrWhiteSpace(model.HtmlContent))
        {
            return ServiceResult.Fail("Vui lòng nhập nội dung HTML của email.");
        }

        try
        {
            await File.WriteAllTextAsync(
                GetTemplatePath(definition.TemplateName),
                model.HtmlContent,
                Encoding.UTF8);

            var subjects = ReadSubjects();
            subjects[definition.TemplateName] = model.Subject.Trim();
            await WriteSubjectsAsync(subjects);

            return ServiceResult.Ok("Đã cập nhật mẫu email thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể cập nhật template email {TemplateName}", definition.TemplateName);
            return ServiceResult.Fail("Không thể lưu mẫu email. Vui lòng thử lại.");
        }
    }

    public string GetSubject(string templateName)
    {
        var definition = FindDefinition(templateName);
        if (definition == null)
        {
            return string.Empty;
        }

        var subjects = ReadSubjects();
        return GetSubject(definition, subjects);
    }

    public string GetSubject(string templateName, Dictionary<string, string> replacements)
    {
        var subject = GetSubject(templateName);
        return ApplyReplacements(subject, replacements);
    }

    private static EmailTemplateDefinition? FindDefinition(string templateName)
    {
        return Definitions.FirstOrDefault(d =>
            d.TemplateName.Equals(templateName, StringComparison.OrdinalIgnoreCase));
    }

    private string GetTemplatePath(string templateName)
    {
        return Path.Combine(GetTemplateRoot(), templateName);
    }

    private FileInfo GetTemplateFileInfo(string templateName)
    {
        return new FileInfo(GetTemplatePath(templateName));
    }

    private string GetTemplateRoot()
    {
        return Path.Combine(_env.WebRootPath, "templates", "emails");
    }

    private string GetSubjectConfigPath()
    {
        return Path.Combine(GetTemplateRoot(), SubjectConfigFileName);
    }

    private Dictionary<string, string> ReadSubjects()
    {
        var path = GetSubjectConfigPath();
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc file cấu hình subject email {Path}", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task WriteSubjectsAsync(Dictionary<string, string> subjects)
    {
        Directory.CreateDirectory(GetTemplateRoot());
        var json = JsonSerializer.Serialize(subjects, JsonOptions);
        await File.WriteAllTextAsync(GetSubjectConfigPath(), json, Encoding.UTF8);
    }

    private static string GetSubject(
        EmailTemplateDefinition definition,
        IReadOnlyDictionary<string, string> subjects)
    {
        return subjects.TryGetValue(definition.TemplateName, out var configuredSubject)
            && !string.IsNullOrWhiteSpace(configuredSubject)
                ? configuredSubject
                : definition.DefaultSubject;
    }

    private static string ApplyReplacements(string content, Dictionary<string, string> replacements)
    {
        var builder = new StringBuilder(content);
        foreach (var replacement in replacements)
        {
            builder.Replace(replacement.Key, replacement.Value ?? string.Empty);
        }

        return builder.ToString();
    }

}
