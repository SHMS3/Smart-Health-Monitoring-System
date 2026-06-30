using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Interfaces;

public interface IEmailTemplateService
{
    IReadOnlyList<EmailTemplateDefinition> GetDefinitions();

    Task<IReadOnlyList<EmailTemplateListItemViewModel>> GetTemplateListAsync();

    Task<EmailTemplateEditViewModel?> GetTemplateForEditAsync(string templateName);

    Task<ServiceResult> UpdateTemplateAsync(EmailTemplateEditViewModel model);

    string GetSubject(string templateName);

    string GetSubject(string templateName, Dictionary<string, string> replacements);

    string RenderBody(string templateName, Dictionary<string, string> replacements);
}
