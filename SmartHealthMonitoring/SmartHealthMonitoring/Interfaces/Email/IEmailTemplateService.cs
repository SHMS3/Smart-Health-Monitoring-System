using SmartHealthMonitoring.ViewModels.Admin;
using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.Interfaces.Email;

public interface IEmailTemplateService
{
    IReadOnlyList<EmailTemplateListItemViewModel> GetTemplateList();
    Task<EmailTemplateEditViewModel?> GetTemplateForEditAsync(string templateName);
    Task<ServiceResult> UpdateTemplateAsync(EmailTemplateEditViewModel model);
    string GetSubject(string templateName);
    string GetSubject(string templateName, Dictionary<string, string> replacements);
}
