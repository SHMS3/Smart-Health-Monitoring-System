using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class EmailTemplateServiceTests
{
    private const string TemplateName = "AppointmentReminderTemplate.html";

    [Fact]
    public async Task GetTemplateForEditAsync_WhenTemplateIsUnknown_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        var result = await service.GetTemplateForEditAsync("../appsettings.json");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTemplateForEditAsync_WhenRegisteredFileIsMissing_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        var result = await service.GetTemplateForEditAsync(TemplateName);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTemplateForEditAsync_LoadsConfiguredSubjectAndDistinctTokens()
    {
        using var temp = new TempDirectory();
        var templateRoot = CreateTemplateRoot(temp.Path);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(templateRoot, TemplateName),
            "<p>{{PatientName}} - {{Code}} - {{PatientName}}</p>");
        await WriteSubjectsAsync(
            templateRoot,
            new Dictionary<string, string>
            {
                [TemplateName] = "Hello {{PatientName}} {{SubjectOnly}}"
            });

        var result = await CreateService(temp.Path).GetTemplateForEditAsync(TemplateName);

        Assert.NotNull(result);
        Assert.Equal("Hello {{PatientName}} {{SubjectOnly}}", result.Subject);
        Assert.Equal(
            ["{{PatientName}}", "{{SubjectOnly}}", "{{Code}}"],
            result.Tokens);
        Assert.NotNull(result.LastModifiedAt);
    }

    [Fact]
    public void GetSubject_WhenSubjectConfigIsMalformed_FallsBackToDefault()
    {
        using var temp = new TempDirectory();
        var templateRoot = CreateTemplateRoot(temp.Path);
        File.WriteAllText(
            System.IO.Path.Combine(templateRoot, "template-subjects.json"),
            "{ invalid json");
        var service = CreateService(temp.Path);

        var result = service.GetSubject(TemplateName);

        Assert.Contains("{{ReminderLabel}}", result);
    }

    [Theory]
    [InlineData("UnknownTemplate.html", "Subject", "<p>Body</p>")]
    [InlineData(TemplateName, "   ", "<p>Body</p>")]
    [InlineData(TemplateName, "Subject", "   ")]
    public async Task UpdateTemplateAsync_WhenInputIsInvalid_ReturnsFailure(
        string templateName,
        string subject,
        string htmlContent)
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var model = new EmailTemplateEditViewModel
        {
            TemplateName = templateName,
            Subject = subject,
            HtmlContent = htmlContent
        };

        var result = await service.UpdateTemplateAsync(model);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task UpdateTemplateAsync_WhenInputIsValid_PersistsHtmlAndSubject()
    {
        using var temp = new TempDirectory();
        var templateRoot = CreateTemplateRoot(temp.Path);
        var service = CreateService(temp.Path);
        var model = new EmailTemplateEditViewModel
        {
            TemplateName = TemplateName,
            Subject = "  Reminder for {{PatientName}}  ",
            HtmlContent = "<h1>Hello {{PatientName}}</h1>"
        };

        var result = await service.UpdateTemplateAsync(model);

        Assert.True(result.Success);
        Assert.Equal(
            model.HtmlContent,
            await File.ReadAllTextAsync(System.IO.Path.Combine(templateRoot, TemplateName)));
        Assert.Equal("Reminder for {{PatientName}}", service.GetSubject(TemplateName));
        Assert.Equal(
            "Reminder for Lan",
            service.GetSubject(
                TemplateName,
                new Dictionary<string, string> { ["{{PatientName}}"] = "Lan" }));
    }

    [Fact]
    public async Task GetTemplateList_ReportsExistingFileAndConfiguredSubject()
    {
        using var temp = new TempDirectory();
        var templateRoot = CreateTemplateRoot(temp.Path);
        var html = "<p>Reminder</p>";
        await File.WriteAllTextAsync(System.IO.Path.Combine(templateRoot, TemplateName), html);
        await WriteSubjectsAsync(
            templateRoot,
            new Dictionary<string, string> { [TemplateName] = "Custom reminder" });

        var result = CreateService(temp.Path).GetTemplateList();

        Assert.Equal(8, result.Count);
        var reminder = result.Single(item => item.TemplateName == TemplateName);
        Assert.Equal("Custom reminder", reminder.Subject);
        Assert.True(reminder.FileSize > 0);
        Assert.NotNull(reminder.LastModifiedAt);
    }

    private static EmailTemplateService CreateService(string rootPath)
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = rootPath,
            WebRootPath = rootPath
        };

        return new EmailTemplateService(
            environment,
            NullLogger<EmailTemplateService>.Instance);
    }

    private static string CreateTemplateRoot(string webRootPath)
    {
        var templateRoot = System.IO.Path.Combine(webRootPath, "templates", "emails");
        Directory.CreateDirectory(templateRoot);
        return templateRoot;
    }

    private static Task WriteSubjectsAsync(
        string templateRoot,
        Dictionary<string, string> subjects)
    {
        return File.WriteAllTextAsync(
            System.IO.Path.Combine(templateRoot, "template-subjects.json"),
            JsonSerializer.Serialize(subjects));
    }
}
