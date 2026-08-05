using SmartHealthMonitoring.Common;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services.Email;

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
            DisplayName = "M?i t�i kh�m",
            Description = "G?i cho b?nh nh�n sau khi b�c si x? l� c?nh b�o v� c?n h?n t�i kh�m.",
            DefaultSubject = "Thu M?i T�i Kh�m - Smart Health Monitoring",
        },
        new()
        {
            TemplateName = "HealthWarningTemplate.html",
            DisplayName = "C?nh b�o s?c kh?e",
            Description = "G?i t? d?ng khi AI ph�t hi?n nguy co s?c kh?e cao.",
            DefaultSubject = "C?NH B�O S?C KH?E KH?N C?P - C?n t?i kh�m ngay",
        },
        new()
        {
            TemplateName = "VitalLogReminderTemplate.html",
            DisplayName = "Nh?c ghi ch? s?",
            Description = "Nh?c b?nh nh�n c?p nh?t ch? s? sinh hi?u h?ng ng�y.",
            DefaultSubject = "NH?C NH?: Vui l�ng ghi nh?n ch? s? s?c kh?e h?ng ng�y - Smart Health",
        },
        new()
        {
            TemplateName = "PatientHealthReportTemplate.html",
            DisplayName = "B�o c�o y t?",
            Description = "M?u b�o c�o t�nh tr?ng y t? g?i cho b?nh nh�n khi c?n.",
            DefaultSubject = "B�o c�o T�nh tr?ng Y t? - Smart Health",
        },
        new()
        {
            TemplateName = "WarningAlertTemplate.html",
            DisplayName = "C?nh b�o ch? s?",
            Description = "M?u c?nh b�o ch? s? s?c kh?e d�ng cho c�c lu?ng c?nh b�o cu.",
            DefaultSubject = "C?NH B�O: Ch? s? s?c kh?e b?t thu?ng - Smart Health",
        },
        new()
        {
            TemplateName = "DoctorAcceptedCheckInTemplate.html",
            DisplayName = "QR Check-in khi b�c si ti?p nh?n",
            Description = "G?i cho b?nh nh�n ngay khi b�c si ti?p nh?n th�nh c�ng trong h�ng d?i kh�m.",
            DefaultSubject = "B�c si d� ti?p nh?n - QR Check-in c?a b?n - Smart Health",
        },
        new()
        {
            TemplateName = "AppointmentBookingConfirmationTemplate.html",
            DisplayName = "X�c nh?n d?t l?ch + QR Check-in",
            Description = "NTF-01: G?i khi l? t�n duy?t d?t l?ch (BOOK-08) th�nh c�ng.",
            DefaultSubject = "X�c nh?n d?t l?ch th�nh c�ng - QR Check-in - Smart Health",
        },
        new()
        {
            TemplateName = "AppointmentReminderTemplate.html",
            DisplayName = "Nh?c l?ch kh�m 24h/2h",
            Description = "NTF-02: Email nh?c tru?c gi? kh�m 24 gi? ho?c 2 gi?.",
            DefaultSubject = "Nh?c l?ch kh�m - c�n {{ReminderLabel}} - Smart Health",
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
            return ServiceResult.Fail("Template kh�ng h?p l?.");
        }

        if (string.IsNullOrWhiteSpace(model.Subject))
        {
            return ServiceResult.Fail("Vui l�ng nh?p ti�u d? email.");
        }

        if (string.IsNullOrWhiteSpace(model.HtmlContent))
        {
            return ServiceResult.Fail("Vui l�ng nh?p n?i dung HTML c?a email.");
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

            return ServiceResult.Ok("�� c?p nh?t m?u email th�nh c�ng.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kh�ng th? c?p nh?t template email {TemplateName}", definition.TemplateName);
            return ServiceResult.Fail("Kh�ng th? luu m?u email. Vui l�ng th? l?i.");
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
            _logger.LogWarning(ex, "Kh�ng th? d?c file c?u h�nh subject email {Path}", path);
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



