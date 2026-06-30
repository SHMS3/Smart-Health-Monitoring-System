using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services;

public class PatientUiSettingsService : IPatientUiSettingsService
{
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private static readonly Regex HexColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PatientUiSettingsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public PatientUiSettingsService(IWebHostEnvironment environment, ILogger<PatientUiSettingsService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<PatientUiSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetSettingsFilePath();
            if (!File.Exists(filePath))
            {
                return CreateDefaultSettings();
            }

            await using var stream = File.OpenRead(filePath);
            var settings = await JsonSerializer.DeserializeAsync<PatientUiSettings>(
                stream,
                _jsonOptions,
                cancellationToken);

            return Normalize(settings ?? CreateDefaultSettings());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot read patient UI settings. Falling back to defaults.");
            return CreateDefaultSettings();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<PatientUiSettings> UpdateSettingsAsync(
        PatientUiSettingsViewModel model,
        string? updatedByAdminName,
        CancellationToken cancellationToken = default)
    {
        var settings = Normalize(new PatientUiSettings
        {
            BrandName = model.BrandName,
            BrandSubtitle = model.BrandSubtitle,
            MetaDescription = model.MetaDescription,
            SearchPlaceholder = model.SearchPlaceholder,
            HomeHeroEyebrow = model.HomeHeroEyebrow,
            HomeHeroTitle = model.HomeHeroTitle,
            HomeHeroHighlight = model.HomeHeroHighlight,
            HomeHeroPriceTag = model.HomeHeroPriceTag,
            HomeHeroPrice = model.HomeHeroPrice,
            HomeHeroPriceSuffix = model.HomeHeroPriceSuffix,
            HomeHeroNote = model.HomeHeroNote,
            HomeHeroImageUrl = model.HomeHeroImageUrl,
            HomeAboutTag = model.HomeAboutTag,
            HomeAboutTitle = model.HomeAboutTitle,
            HomeAboutDescription = model.HomeAboutDescription,
            HomeAboutImageUrl = model.HomeAboutImageUrl,
            HomeCtaTitle = model.HomeCtaTitle,
            HomeCtaDescription = model.HomeCtaDescription,
            PrimaryColor = model.PrimaryColor,
            PrimaryDarkColor = model.PrimaryDarkColor,
            NavigationColor = model.NavigationColor,
            AccentColor = model.AccentColor,
            BackgroundColor = model.BackgroundColor,
            LogoIcon = model.LogoIcon,
            HotlinePhone = model.HotlinePhone,
            HotlineLabel = model.HotlineLabel,
            ContactEmail = model.ContactEmail,
            Address = model.Address,
            FooterSubtitle = model.FooterSubtitle,
            FooterDescription = model.FooterDescription,
            FooterLicenseText = model.FooterLicenseText,
            ShowTopInfoBar = model.ShowTopInfoBar,
            ShowAiChatbot = model.ShowAiChatbot,
            ShowSupportHub = model.ShowSupportHub,
            UpdatedAt = DateTime.UtcNow,
            UpdatedByAdminName = updatedByAdminName
        });

        await SaveSettingsAsync(settings, cancellationToken);
        return settings;
    }

    public async Task<PatientUiSettings> ResetToDefaultAsync(
        string? updatedByAdminName,
        CancellationToken cancellationToken = default)
    {
        var settings = CreateDefaultSettings();
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminName = updatedByAdminName;

        await SaveSettingsAsync(settings, cancellationToken);
        return settings;
    }

    private async Task SaveSettingsAsync(PatientUiSettings settings, CancellationToken cancellationToken)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private string GetSettingsFilePath()
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "patient-ui-settings.json");
    }

    private static PatientUiSettings CreateDefaultSettings()
    {
        return new PatientUiSettings();
    }

    private static PatientUiSettings Normalize(PatientUiSettings settings)
    {
        var defaults = CreateDefaultSettings();

        settings.BrandName = TrimOrDefault(settings.BrandName, defaults.BrandName, 80);
        settings.BrandSubtitle = TrimOrDefault(settings.BrandSubtitle, defaults.BrandSubtitle, 80);
        settings.MetaDescription = TrimOrDefault(settings.MetaDescription, defaults.MetaDescription, 180);
        settings.SearchPlaceholder = TrimOrDefault(settings.SearchPlaceholder, defaults.SearchPlaceholder, 120);
        settings.HomeHeroEyebrow = TrimOrDefault(settings.HomeHeroEyebrow, defaults.HomeHeroEyebrow, 100);
        settings.HomeHeroTitle = TrimOrDefault(settings.HomeHeroTitle, defaults.HomeHeroTitle, 120);
        settings.HomeHeroHighlight = TrimOrDefault(settings.HomeHeroHighlight, defaults.HomeHeroHighlight, 120);
        settings.HomeHeroPriceTag = TrimOrDefault(settings.HomeHeroPriceTag, defaults.HomeHeroPriceTag, 80);
        settings.HomeHeroPrice = TrimOrDefault(settings.HomeHeroPrice, defaults.HomeHeroPrice, 60);
        settings.HomeHeroPriceSuffix = TrimOrDefault(settings.HomeHeroPriceSuffix, defaults.HomeHeroPriceSuffix, 80);
        settings.HomeHeroNote = TrimOrDefault(settings.HomeHeroNote, defaults.HomeHeroNote, 140);
        settings.HomeHeroImageUrl = NormalizeImageUrl(settings.HomeHeroImageUrl, defaults.HomeHeroImageUrl);
        settings.HomeAboutTag = TrimOrDefault(settings.HomeAboutTag, defaults.HomeAboutTag, 80);
        settings.HomeAboutTitle = TrimOrDefault(settings.HomeAboutTitle, defaults.HomeAboutTitle, 120);
        settings.HomeAboutDescription = TrimOrDefault(settings.HomeAboutDescription, defaults.HomeAboutDescription, 500);
        settings.HomeAboutImageUrl = NormalizeImageUrl(settings.HomeAboutImageUrl, defaults.HomeAboutImageUrl);
        settings.HomeCtaTitle = TrimOrDefault(settings.HomeCtaTitle, defaults.HomeCtaTitle, 120);
        settings.HomeCtaDescription = TrimOrDefault(settings.HomeCtaDescription, defaults.HomeCtaDescription, 220);
        settings.PrimaryColor = NormalizeColor(settings.PrimaryColor, defaults.PrimaryColor);
        settings.PrimaryDarkColor = NormalizeColor(settings.PrimaryDarkColor, defaults.PrimaryDarkColor);
        settings.NavigationColor = NormalizeColor(settings.NavigationColor, defaults.NavigationColor);
        settings.AccentColor = NormalizeColor(settings.AccentColor, defaults.AccentColor);
        settings.BackgroundColor = NormalizeColor(settings.BackgroundColor, defaults.BackgroundColor);
        settings.LogoIcon = PatientUiSettingsViewModel.AllowedLogoIcons.Contains(settings.LogoIcon)
            ? settings.LogoIcon
            : defaults.LogoIcon;
        settings.HotlinePhone = TrimOrDefault(settings.HotlinePhone, defaults.HotlinePhone, 30);
        settings.HotlineLabel = TrimOrDefault(settings.HotlineLabel, defaults.HotlineLabel, 40);
        settings.ContactEmail = TrimOrDefault(settings.ContactEmail, defaults.ContactEmail, 120);
        settings.Address = TrimOrDefault(settings.Address, defaults.Address, 160);
        settings.FooterSubtitle = TrimOrDefault(settings.FooterSubtitle, defaults.FooterSubtitle, 80);
        settings.FooterDescription = TrimOrDefault(settings.FooterDescription, defaults.FooterDescription, 260);
        settings.FooterLicenseText = TrimOrDefault(settings.FooterLicenseText, defaults.FooterLicenseText, 180);

        return settings;
    }

    private static string TrimOrDefault(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return HexColorRegex.IsMatch(normalized) ? normalized.ToLowerInvariant() : fallback;
    }

    private static string NormalizeImageUrl(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        return normalized.StartsWith("/") && !normalized.Contains("..") ? normalized : fallback;
    }
}
