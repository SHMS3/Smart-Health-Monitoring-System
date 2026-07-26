using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.UnitTests;

public class PatientUiSettingsServiceTests
{
    [Fact]
    public async Task GetSettingsAsync_WhenFileDoesNotExist_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        var result = await service.GetSettingsAsync();
        var defaults = new PatientUiSettings();

        Assert.Equal(defaults.BrandName, result.BrandName);
        Assert.Equal(defaults.PrimaryColor, result.PrimaryColor);
        Assert.NotEmpty(result.FooterSections);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenJsonIsMalformed_ReturnsDefaults()
    {
        using var temp = new TempDirectory();
        var settingsPath = CreateSettingsPath(temp.Path);
        await File.WriteAllTextAsync(settingsPath, "{ invalid json");

        var result = await CreateService(temp.Path).GetSettingsAsync();

        Assert.Equal(new PatientUiSettings().BrandName, result.BrandName);
        Assert.NotEmpty(result.FooterSections);
    }

    [Fact]
    public async Task GetSettingsAsync_NormalizesUnsafeAndInvalidValues()
    {
        using var temp = new TempDirectory();
        var defaults = new PatientUiSettings();
        var stored = new PatientUiSettings
        {
            BrandName = "  Cardio Center  ",
            PrimaryColor = "  #AABBCC  ",
            AccentColor = "red",
            LogoIcon = "not-an-allowed-icon",
            HomeHeroImageUrl = "/images/../appsettings.json",
            FooterSocialLinks =
            [
                new PatientFooterLink
                {
                    Label = "Unsafe",
                    Url = "javascript:alert(1)"
                }
            ],
            FooterSections =
            [
                new PatientFooterSection
                {
                    Title = "Map",
                    DisplayType = PatientFooterSectionDisplayTypes.Map,
                    MapEmbedUrl = "http://example.com/map"
                }
            ]
        };

        var settingsPath = CreateSettingsPath(temp.Path);
        await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(stored));

        var result = await CreateService(temp.Path).GetSettingsAsync();

        Assert.Equal("Cardio Center", result.BrandName);
        Assert.Equal("#aabbcc", result.PrimaryColor);
        Assert.Equal(defaults.AccentColor, result.AccentColor);
        Assert.Equal(defaults.LogoIcon, result.LogoIcon);
        Assert.Equal(defaults.HomeHeroImageUrl, result.HomeHeroImageUrl);
        Assert.Equal("#", Assert.Single(result.FooterSocialLinks).Url);
        Assert.Equal(string.Empty, Assert.Single(result.FooterSections).MapEmbedUrl);
    }

    [Fact]
    public async Task UpdateSettingsAsync_NormalizesAndPersistsEditableSettings()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var model = PatientUiSettingsViewModel.FromSettings(new PatientUiSettings());
        model.BrandName = "  New Smart Health  ";
        model.PrimaryColor = "#AABBCC";
        model.HomeHeroImageUrl = "/images/new-hero.png";
        model.WorkScheduleItems = Enumerable.Range(1, 13)
            .Select(index => new PatientFooterEditorItemViewModel
            {
                Label = $"Shift {index}",
                Value = "08:00 - 17:00"
            })
            .ToList();
        model.ContactItems =
        [
            new PatientFooterEditorItemViewModel
            {
                Label = "Phone",
                Value = "+84 123-456",
                Kind = PatientFooterItemKinds.Phone
            },
            new PatientFooterEditorItemViewModel
            {
                Label = "Email",
                Value = " contact@example.com ",
                Kind = PatientFooterItemKinds.Email
            },
            new PatientFooterEditorItemViewModel
            {
                Label = "Website",
                Value = "example.com",
                Kind = PatientFooterItemKinds.Website
            },
            new PatientFooterEditorItemViewModel
            {
                Label = "Status",
                Value = "Online",
                Kind = PatientFooterItemKinds.Status
            }
        ];

        var result = await service.UpdateSettingsAsync(model, "Admin Nam");

        Assert.Equal("New Smart Health", result.BrandName);
        Assert.Equal("#aabbcc", result.PrimaryColor);
        Assert.Equal("/images/new-hero.png", result.HomeHeroImageUrl);
        Assert.Equal("Admin Nam", result.UpdatedByAdminName);

        var schedule = result.FooterSections.Single(
            section => section.DisplayType == PatientFooterSectionDisplayTypes.Schedule);
        Assert.Equal(12, schedule.Items.Count);

        var contact = result.FooterSections.Single(
            section => section.DisplayType == PatientFooterSectionDisplayTypes.Contact);
        Assert.Equal("tel:+84123456", contact.Items.Single(item => item.Label == "Phone").Url);
        Assert.Equal("mailto:contact@example.com", contact.Items.Single(item => item.Label == "Email").Url);
        Assert.Equal("https://example.com", contact.Items.Single(item => item.Label == "Website").Url);
        Assert.True(contact.Items.Single(item => item.Label == "Status").Highlight);

        var reloaded = await service.GetSettingsAsync();
        Assert.Equal(result.BrandName, reloaded.BrandName);
        Assert.Equal(result.PrimaryColor, reloaded.PrimaryColor);
        Assert.Equal(result.UpdatedByAdminName, reloaded.UpdatedByAdminName);
    }

    [Fact]
    public async Task ResetToDefaultAsync_ReplacesStoredValuesAndPersistsAdmin()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var settingsPath = CreateSettingsPath(temp.Path);
        await File.WriteAllTextAsync(
            settingsPath,
            JsonSerializer.Serialize(new PatientUiSettings { BrandName = "Modified" }));

        var reset = await service.ResetToDefaultAsync("Reset Admin");
        var reloaded = await service.GetSettingsAsync();

        Assert.Equal(new PatientUiSettings().BrandName, reset.BrandName);
        Assert.Equal("Reset Admin", reset.UpdatedByAdminName);
        Assert.Equal(reset.BrandName, reloaded.BrandName);
        Assert.Equal(reset.UpdatedByAdminName, reloaded.UpdatedByAdminName);
    }

    private static PatientUiSettingsService CreateService(string contentRootPath)
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = contentRootPath,
            WebRootPath = contentRootPath
        };

        return new PatientUiSettingsService(
            environment,
            NullLogger<PatientUiSettingsService>.Instance);
    }

    private static string CreateSettingsPath(string contentRootPath)
    {
        var appDataPath = System.IO.Path.Combine(contentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);
        return System.IO.Path.Combine(appDataPath, "patient-ui-settings.json");
    }
}
