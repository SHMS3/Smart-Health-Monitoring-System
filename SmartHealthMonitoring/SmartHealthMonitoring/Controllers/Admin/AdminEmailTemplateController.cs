using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin;

[Authorize(Roles = "2")]
public class AdminEmailTemplateController : Controller
{
    private readonly EmailTemplateService _emailTemplateService;

    public AdminEmailTemplateController(EmailTemplateService emailTemplateService)
    {
        _emailTemplateService = emailTemplateService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = _emailTemplateService.GetTemplateList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string templateName)
    {
        var model = await _emailTemplateService.GetTemplateForEditAsync(templateName);
        if (model == null)
        {
            TempData["Error"] = "Không tìm thấy mẫu email.";
            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmailTemplateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateTemplateMetadataAsync(model);
            return View(model);
        }

        var result = await _emailTemplateService.UpdateTemplateAsync(model);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            await PopulateTemplateMetadataAsync(model);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Edit), new { templateName = model.TemplateName });
    }

    private async Task PopulateTemplateMetadataAsync(EmailTemplateEditViewModel model)
    {
        var original = await _emailTemplateService.GetTemplateForEditAsync(model.TemplateName);
        if (original == null)
        {
            return;
        }

        model.DisplayName = original.DisplayName;
        model.Description = original.Description;
        model.Tokens = original.Tokens;
        model.LastModifiedAt = original.LastModifiedAt;
    }
}
