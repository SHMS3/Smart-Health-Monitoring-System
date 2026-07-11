using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private const string SubjectConfigFileName = "template-subjects.json";
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EmailTemplateService> _logger;
    private static readonly Regex BodyRegex = new(
        @"<body(?<attrs>[^>]*)>(?<content>[\s\S]*?)</body>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            IsUsedInSystem = true,
            Tokens = new()
            {
                "{{PatientName}}",
                "{{AppointmentMessage}}",
                "{{DoctorName}}",
                "{{HospitalReplyContact}}",
                "{{LastExamDate}}",
                "{{AppointmentDate}}"
            }
        },
        new()
        {
            TemplateName = "HealthWarningTemplate.html",
            DisplayName = "Cảnh báo sức khỏe",
            Description = "Gửi tự động khi AI phát hiện nguy cơ sức khỏe cao.",
            DefaultSubject = "CẢNH BÁO SỨC KHỎE KHẨN CẤP - Cần tới khám ngay",
            IsUsedInSystem = true,
            Tokens = new()
            {
                "{{PatientName}}",
                "{{RiskScore}}",
                "{{RiskLevel}}",
                "{{DetectedAt}}"
            }
        },
        new()
        {
            TemplateName = "SosEmergencyContactTemplate.html",
            DisplayName = "SOS người thân",
            Description = "Gửi tự động cho người thân khi bệnh nhân đạt ngưỡng SOS.",
            DefaultSubject = "[SOS] Cảnh báo khẩn cấp cho {{PatientName}}",
            IsUsedInSystem = true,
            Tokens = new()
            {
                "{{ContactName}}",
                "{{PatientName}}",
                "{{DetectedAt}}",
                "{{RiskScore}}",
                "{{RiskLevel}}"
            }
        },
        new()
        {
            TemplateName = "VitalLogReminderTemplate.html",
            DisplayName = "Nhắc ghi chỉ số",
            Description = "Nhắc bệnh nhân cập nhật chỉ số sinh hiệu hằng ngày.",
            DefaultSubject = "NHẮC NHỞ: Vui lòng ghi nhận chỉ số sức khỏe hằng ngày - Smart Health",
            IsUsedInSystem = true,
            Tokens = new()
            {
                "{{PatientName}}",
                "{{LastLogTimeDisplay}}",
                "{{ActionUrl}}"
            }
        },
        new()
        {
            TemplateName = "PatientHealthReportTemplate.html",
            DisplayName = "Báo cáo y tế",
            Description = "Mẫu báo cáo tình trạng y tế gửi cho bệnh nhân khi cần.",
            DefaultSubject = "Báo cáo Tình trạng Y tế - Smart Health",
            IsUsedInSystem = false,
            Tokens = new()
            {
                "{{PatientName}}",
                "{{RecordDate}}",
                "{{Diagnosis}}",
                "{{Severity}}",
                "{{VitalSigns}}",
                "{{DoctorAdvice}}"
            }
        },
        new()
        {
            TemplateName = "WarningAlertTemplate.html",
            DisplayName = "Cảnh báo chỉ số",
            Description = "Mẫu cảnh báo chỉ số sức khỏe dùng cho các luồng cảnh báo cũ.",
            DefaultSubject = "CẢNH BÁO: Chỉ số sức khỏe bất thường - Smart Health",
            IsUsedInSystem = false,
            Tokens = new()
            {
                "{{PatientName}}",
                "{{AlertMessage}}",
                "{{RiskLevel}}",
                "{{DetectedAt}}"
            }
        }
    };

    public EmailTemplateService(IWebHostEnvironment env, ILogger<EmailTemplateService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public IReadOnlyList<EmailTemplateDefinition> GetDefinitions() => Definitions;

    public async Task<IReadOnlyList<EmailTemplateListItemViewModel>> GetTemplateListAsync()
    {
        var subjects = await ReadSubjectsAsync();

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
                    TokenCount = definition.Tokens.Count,
                    IsUsedInSystem = definition.IsUsedInSystem
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

        var subjects = await ReadSubjectsAsync();
        var htmlContent = await File.ReadAllTextAsync(fileInfo.FullName, Encoding.UTF8);
        var sampleReplacements = BuildSampleReplacements(definition);

        return new EmailTemplateEditViewModel
        {
            TemplateName = definition.TemplateName,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            Subject = GetSubject(definition, subjects),
            HtmlContent = htmlContent,
            BodyContent = ExtractBodyContent(htmlContent),
            Tokens = definition.Tokens.ToList(),
            LastModifiedAt = fileInfo.LastWriteTime,
            PreviewHtml = ApplyReplacements(htmlContent, sampleReplacements),
            IsUsedInSystem = definition.IsUsedInSystem
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

        var isVisualMode = model.EditorMode.Equals("visual", StringComparison.OrdinalIgnoreCase);
        if (isVisualMode && string.IsNullOrWhiteSpace(model.BodyContent))
        {
            return ServiceResult.Fail("Vui lòng nhập nội dung email.");
        }

        if (!isVisualMode && string.IsNullOrWhiteSpace(model.HtmlContent))
        {
            return ServiceResult.Fail("Vui lòng nhập nội dung HTML của email.");
        }

        try
        {
            var templatePath = GetTemplatePath(definition.TemplateName);
            var htmlToSave = model.HtmlContent;
            if (isVisualMode)
            {
                var baseHtml = !string.IsNullOrWhiteSpace(model.HtmlContent)
                    ? model.HtmlContent
                    : await File.ReadAllTextAsync(templatePath, Encoding.UTF8);

                htmlToSave = ReplaceBodyContent(baseHtml, model.BodyContent);
            }

            await File.WriteAllTextAsync(templatePath, htmlToSave, Encoding.UTF8);

            var subjects = await ReadSubjectsAsync();
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

    public string RenderBody(string templateName, Dictionary<string, string> replacements)
    {
        var definition = FindDefinition(templateName);
        if (definition == null)
        {
            _logger.LogWarning("Template email không nằm trong danh sách cho phép: {TemplateName}", templateName);
            return string.Empty;
        }

        var templatePath = GetTemplatePath(definition.TemplateName);
        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("Không tìm thấy template email {TemplateName} tại {TemplatePath}", templateName, templatePath);
            return string.Empty;
        }

        var htmlContent = File.ReadAllText(templatePath, Encoding.UTF8);
        return ApplyReplacements(htmlContent, replacements);
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

    private async Task<Dictionary<string, string>> ReadSubjectsAsync()
    {
        var path = GetSubjectConfigPath();
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đọc file cấu hình subject email {Path}", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
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

    private static Dictionary<string, string> BuildSampleReplacements(EmailTemplateDefinition definition)
    {
        var samples = new Dictionary<string, string>
        {
            ["{{PatientName}}"] = "Nguyễn Văn An",
            ["{{AppointmentMessage}}"] = "Bác sĩ khuyến nghị tái khám để đánh giá lại các chỉ số gần đây.",
            ["{{DoctorName}}"] = "Trần Minh Khoa",
            ["{{ContactName}}"] = "Nguyễn Văn A",
            ["{{HospitalReplyContact}}"] = "smarthealth.support@gmail.com | 1900-9999",
            ["{{LastExamDate}}"] = DateTime.Now.AddDays(-7).ToString("dd/MM/yyyy"),
            ["{{AppointmentDate}}"] = DateTime.Now.AddDays(3).ToString("dd/MM/yyyy HH:mm"),
            ["{{RiskScore}}"] = "82.50",
            ["{{RiskLevel}}"] = "3",
            ["{{DetectedAt}}"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
            ["{{LastLogTimeDisplay}}"] = DateTime.Now.AddHours(-2).ToString("dd/MM/yyyy HH:mm"),
            ["{{ActionUrl}}"] = "http://localhost:5033/Patient/Create",
            ["{{RecordDate}}"] = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            ["{{Diagnosis}}"] = "Kết quả khám lâm sàng",
            ["{{Severity}}"] = "Cần theo dõi",
            ["{{VitalSigns}}"] = "Nhịp tim: 92 bpm | Huyết áp: 145/90 mmHg",
            ["{{DoctorAdvice}}"] = "Theo dõi chỉ số hằng ngày và tái khám đúng lịch.",
            ["{{AlertMessage}}"] = "Hệ thống phát hiện chỉ số sức khỏe bất thường."
        };

        return definition.Tokens
            .Where(samples.ContainsKey)
            .ToDictionary(token => token, token => samples[token]);
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

    private static string ExtractBodyContent(string htmlContent)
    {
        var match = BodyRegex.Match(htmlContent);
        return match.Success
            ? match.Groups["content"].Value.Trim()
            : htmlContent;
    }

    private static string ReplaceBodyContent(string htmlContent, string bodyContent)
    {
        var match = BodyRegex.Match(htmlContent);
        if (!match.Success)
        {
            return bodyContent;
        }

        var contentGroup = match.Groups["content"];
        return htmlContent[..contentGroup.Index]
            + Environment.NewLine
            + bodyContent.Trim()
            + Environment.NewLine
            + htmlContent[(contentGroup.Index + contentGroup.Length)..];
    }
}
