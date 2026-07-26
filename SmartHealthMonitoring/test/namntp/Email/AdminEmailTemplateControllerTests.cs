using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHealthMonitoring.Controllers.Admin;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class AdminEmailTemplateControllerTests
{
    private const string TemplateName = "AppointmentReminderTemplate.html";

    [Fact]
    public void Index_ReturnsAllRegisteredTemplates()
    {
        using var temp = new TempDirectory();
        var controller = CreateController(temp.Path);

        var result = controller.Index();

        var templates = Assert.IsAssignableFrom<IReadOnlyList<EmailTemplateListItemViewModel>>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(8, templates.Count);
    }

    [Fact]
    public async Task EditGet_WhenTemplateIsUnknown_RedirectsWithError()
    {
        using var temp = new TempDirectory();
        var controller = CreateController(temp.Path);

        var result = await controller.Edit("unknown.html");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminEmailTemplateController.Index), redirect.ActionName);
        Assert.NotNull(controller.TempData["Error"]);
    }

    [Fact]
    public async Task EditGet_WhenTemplateExists_ReturnsEditorModel()
    {
        using var temp = new TempDirectory();
        await CreateTemplateAsync(temp.Path, "<p>{{PatientName}}</p>");
        var controller = CreateController(temp.Path);

        var result = await controller.Edit(TemplateName);

        var model = Assert.IsType<EmailTemplateEditViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(TemplateName, model.TemplateName);
        Assert.Contains("{{PatientName}}", model.Tokens);
    }

    [Fact]
    public async Task EditPost_WhenModelStateIsInvalid_RepopulatesMetadata()
    {
        using var temp = new TempDirectory();
        await CreateTemplateAsync(temp.Path, "<p>{{PatientName}}</p>");
        var controller = CreateController(temp.Path);
        controller.ModelState.AddModelError(nameof(EmailTemplateEditViewModel.Subject), "Required");
        var model = new EmailTemplateEditViewModel
        {
            TemplateName = TemplateName,
            Subject = string.Empty,
            HtmlContent = "<p>Body</p>"
        };

        var result = await controller.Edit(model);

        var returned = Assert.IsType<EmailTemplateEditViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.NotEmpty(returned.DisplayName);
        Assert.Contains("{{PatientName}}", returned.Tokens);
    }

    [Fact]
    public async Task EditPost_WhenUpdateSucceeds_RedirectsBackToSameTemplate()
    {
        using var temp = new TempDirectory();
        await CreateTemplateAsync(temp.Path, "<p>Old</p>");
        var controller = CreateController(temp.Path);
        var model = new EmailTemplateEditViewModel
        {
            TemplateName = TemplateName,
            Subject = "Updated subject",
            HtmlContent = "<p>Updated</p>"
        };

        var result = await controller.Edit(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminEmailTemplateController.Edit), redirect.ActionName);
        Assert.Equal(TemplateName, redirect.RouteValues!["templateName"]);
        Assert.NotNull(controller.TempData["Success"]);
    }

    [Fact]
    public async Task EditPost_WhenServiceRejectsTemplate_ReturnsViewWithError()
    {
        using var temp = new TempDirectory();
        var controller = CreateController(temp.Path);
        var model = new EmailTemplateEditViewModel
        {
            TemplateName = "unknown.html",
            Subject = "Subject",
            HtmlContent = "<p>Body</p>"
        };

        var result = await controller.Edit(model);

        Assert.IsType<ViewResult>(result);
        Assert.NotNull(controller.TempData["Error"]);
    }

    private static AdminEmailTemplateController CreateController(string webRootPath)
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = webRootPath,
            WebRootPath = webRootPath
        };
        var service = new EmailTemplateService(
            environment,
            NullLogger<EmailTemplateService>.Instance);

        return new AdminEmailTemplateController(service).WithUser(1, roles: ["2"]);
    }

    private static async Task CreateTemplateAsync(string webRootPath, string html)
    {
        var templateRoot = System.IO.Path.Combine(webRootPath, "templates", "emails");
        Directory.CreateDirectory(templateRoot);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(templateRoot, TemplateName),
            html);
    }
}
