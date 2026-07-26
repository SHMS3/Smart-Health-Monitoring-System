using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartHealthMonitoring.Controllers.Admin;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class AdminPatientInterfaceControllerTests
{
    [Fact]
    public async Task Index_ReturnsEditorWithCurrentSettings()
    {
        using var temp = new TempDirectory();
        var setup = CreateController(temp.Path);

        var result = await setup.Controller.Index(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Editor", view.ViewName);
        var model = Assert.IsType<PatientUiSettingsViewModel>(view.Model);
        Assert.Equal(new PatientUiSettings().BrandName, model.BrandName);
        Assert.NotEmpty(model.LogoIconOptions);
    }

    [Fact]
    public async Task Save_WithInvalidLogoIcon_ReturnsEditorWithoutAudit()
    {
        using var temp = new TempDirectory();
        var setup = CreateController(temp.Path);
        var model = ValidModel();
        model.LogoIcon = "unsafe-icon";

        var result = await setup.Controller.Save(model, CancellationToken.None);

        Assert.Equal("Editor", Assert.IsType<ViewResult>(result).ViewName);
        Assert.False(setup.Controller.ModelState.IsValid);
        setup.Audit.Verify(
            service => service.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task Save_WithUnsupportedImageExtension_ReturnsValidationError()
    {
        using var temp = new TempDirectory();
        var setup = CreateController(temp.Path);
        var model = ValidModel();
        model.HomeHeroImageFile = FormFile("malware.exe", [1, 2, 3]);

        var result = await setup.Controller.Save(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.True(setup.Controller.ModelState.ContainsKey(nameof(model.HomeHeroImageFile)));
        Assert.Empty(Directory.GetFiles(temp.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Save_WithValidImages_PersistsFilesSettingsAndAudit()
    {
        using var temp = new TempDirectory();
        var setup = CreateController(temp.Path, fullName: "Admin Nam");
        var model = ValidModel();
        model.BrandName = "Updated Brand";
        model.HomeHeroImageFile = FormFile("hero.PNG", [1, 2, 3, 4]);
        model.HomeAboutImageFile = FormFile("about.webp", [5, 6, 7]);

        var result = await setup.Controller.Save(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.StartsWith("/images/patient-ui/", model.HomeHeroImageUrl);
        Assert.EndsWith(".png", model.HomeHeroImageUrl);
        Assert.StartsWith("/images/patient-ui/", model.HomeAboutImageUrl);
        Assert.EndsWith(".webp", model.HomeAboutImageUrl);
        Assert.Equal(
            2,
            Directory.GetFiles(
                System.IO.Path.Combine(temp.Path, "images", "patient-ui")).Length);

        var saved = await setup.Settings.GetSettingsAsync();
        Assert.Equal("Updated Brand", saved.BrandName);
        Assert.Equal("Admin Nam", saved.UpdatedByAdminName);
        setup.Audit.Verify(service => service.LogAsync(
            "Update",
            "PatientInterface",
            "patient-ui-settings",
            It.Is<string>(description => description.Contains("Admin Nam")),
            null,
            "Admin Nam"), Times.Once);
    }

    [Fact]
    public async Task Reset_RestoresDefaultsAndWritesAudit()
    {
        using var temp = new TempDirectory();
        var setup = CreateController(temp.Path, fullName: "Reset Admin");
        var changed = ValidModel();
        changed.BrandName = "Changed";
        await setup.Settings.UpdateSettingsAsync(changed, "Initial");

        var result = await setup.Controller.Reset(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(
            new PatientUiSettings().BrandName,
            (await setup.Settings.GetSettingsAsync()).BrandName);
        setup.Audit.Verify(service => service.LogAsync(
            "Reset",
            "PatientInterface",
            "patient-ui-settings",
            It.IsAny<string>(),
            null,
            "Reset Admin"), Times.Once);
    }

    private static ControllerSetup CreateController(
        string rootPath,
        string fullName = "Admin")
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = rootPath,
            WebRootPath = rootPath
        };
        var settings = new PatientUiSettingsService(
            environment,
            NullLogger<PatientUiSettingsService>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(service => service.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        var controller = new AdminPatientInterfaceController(
            settings,
            audit.Object,
            environment)
            .WithUser(1, fullName, roles: ["2"]);

        return new ControllerSetup(controller, settings, audit);
    }

    private static PatientUiSettingsViewModel ValidModel()
    {
        return PatientUiSettingsViewModel.FromSettings(new PatientUiSettings());
    }

    private static FormFile FormFile(string fileName, byte[] content)
    {
        return new FormFile(
            new MemoryStream(content),
            0,
            content.Length,
            "image",
            fileName);
    }

    private sealed record ControllerSetup(
        AdminPatientInterfaceController Controller,
        PatientUiSettingsService Settings,
        Mock<IAuditLogService> Audit);
}
