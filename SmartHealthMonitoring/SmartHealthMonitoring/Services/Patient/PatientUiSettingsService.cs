using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services.Patient;

public class PatientUiSettingsService
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
            _logger.LogError(ex, "Cannot read SmartHealthMonitoring.Models.Patient UI settings. Falling back to defaults.");
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
        var current = await GetSettingsAsync(cancellationToken);
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
            FooterBottomText = model.FooterBottomText,
            FooterSocialLinks = current.FooterSocialLinks,
            FooterSections = ApplyEditableFooterItems(
                current.FooterSections,
                model.WorkScheduleItems,
                model.ContactItems),
            FooterBottomLinks = current.FooterBottomLinks,
            ShowTopInfoBar = model.ShowTopInfoBar,
            ShowAiChatbot = model.ShowAiChatbot,
            ShowSupportHub = model.ShowSupportHub,
            UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now,
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
        settings.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;
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
        settings.FooterBottomText = TrimOrDefault(settings.FooterBottomText, defaults.FooterBottomText, 180);
        settings.FooterSocialLinks = NormalizeFooterLinks(settings.FooterSocialLinks, defaults.FooterSocialLinks, 8);
        settings.FooterSections = NormalizeFooterSections(settings.FooterSections, defaults.FooterSections);
        settings.FooterBottomLinks = NormalizeFooterLinks(settings.FooterBottomLinks, defaults.FooterBottomLinks, 6);
        return settings;
    }

    private static List<PatientFooterSection> NormalizeFooterSections(
        List<PatientFooterSection>? sections,
        List<PatientFooterSection> fallback)
    {
        return (sections ?? fallback)
            .Where(section => !string.IsNullOrWhiteSpace(section.Title)
                || !string.IsNullOrWhiteSpace(section.MapEmbedUrl)
                || (section.Items?.Count > 0))
            .Take(6)
            .Select(section =>
            {
                var displayType = section.DisplayType switch
                {
                    PatientFooterSectionDisplayTypes.Schedule => PatientFooterSectionDisplayTypes.Schedule,
                    PatientFooterSectionDisplayTypes.Map => PatientFooterSectionDisplayTypes.Map,
                    _ => PatientFooterSectionDisplayTypes.Contact
                };

                return new PatientFooterSection
                {
                    Title = TrimOrDefault(section.Title, "Footer", 80),
                    IconClass = TrimOrDefault(section.IconClass, "fas fa-circle-info", 80),
                    DisplayType = displayType,
                    MapEmbedUrl = displayType == PatientFooterSectionDisplayTypes.Map
                        ? NormalizeMapUrl(section.MapEmbedUrl)
                        : string.Empty,
                    Items = displayType == PatientFooterSectionDisplayTypes.Map
                        ? new List<PatientFooterItem>()
                        : NormalizeFooterItems(section.Items)
                };
            })
            .ToList();
    }

    private static List<PatientFooterSection> ApplyEditableFooterItems(
        List<PatientFooterSection> currentSections,
        List<PatientFooterEditorItemViewModel> scheduleItems,
        List<PatientFooterEditorItemViewModel> contactItems)
    {
        var sections = currentSections.ToList();
        ReplaceOrAddSection(
            sections,
            PatientFooterSectionDisplayTypes.Schedule,
            "L?ch l�m vi?c",
            "fas fa-clock",
            ToFooterItems(scheduleItems, isContact: false));
        ReplaceOrAddSection(
            sections,
            PatientFooterSectionDisplayTypes.Contact,
            "Li�n h?",
            "fas fa-map-marker-alt",
            ToFooterItems(contactItems, isContact: true));
        return sections;
    }

    private static void ReplaceOrAddSection(
        List<PatientFooterSection> sections,
        string displayType,
        string defaultTitle,
        string defaultIcon,
        List<PatientFooterItem> items)
    {
        var index = sections.FindIndex(section => section.DisplayType == displayType);
        var current = index >= 0 ? sections[index] : null;
        var updated = new PatientFooterSection
        {
            Title = current?.Title ?? defaultTitle,
            IconClass = current?.IconClass ?? defaultIcon,
            DisplayType = displayType,
            Items = items
        };

        if (index >= 0)
            sections[index] = updated;
        else
            sections.Add(updated);
    }

    private static List<PatientFooterItem> ToFooterItems(
        List<PatientFooterEditorItemViewModel>? items,
        bool isContact)
    {
        return (items ?? new List<PatientFooterEditorItemViewModel>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Label) || !string.IsNullOrWhiteSpace(item.Value))
            .Take(12)
            .Select(item => CreateFooterItem(item, isContact))
            .ToList();
    }

    private static PatientFooterItem CreateFooterItem(
        PatientFooterEditorItemViewModel item,
        bool isContact)
    {
        var kind = item.Kind ?? PatientFooterItemKinds.Text;
        var value = item.Value?.Trim() ?? string.Empty;
        var icon = "fas fa-circle";
        var url = string.Empty;

        if (kind == PatientFooterItemKinds.Phone)
        {
            icon = "fas fa-phone-alt";
            if (!string.IsNullOrWhiteSpace(value))
                url = $"tel:{Regex.Replace(value, "[^0-9+]", string.Empty)}";
        }
        else if (isContact && kind == PatientFooterItemKinds.Email)
        {
            icon = "fas fa-envelope";
            if (!string.IsNullOrWhiteSpace(value))
                url = $"mailto:{value}";
        }
        else if (isContact && kind == PatientFooterItemKinds.Address)
        {
            icon = "fas fa-map-marker-alt";
        }
        else if (isContact && kind == PatientFooterItemKinds.Website)
        {
            icon = "fas fa-globe";
            if (!string.IsNullOrWhiteSpace(value))
            {
                url = value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? value
                        : $"https://{value}";
            }
        }
        else if (isContact && kind == PatientFooterItemKinds.Status)
        {
            icon = "fas fa-circle";
        }

        return new PatientFooterItem
        {
            Label = item.Label?.Trim() ?? string.Empty,
            Value = value,
            IconClass = icon,
            Url = url,
            Highlight = item.Highlight || kind == PatientFooterItemKinds.Status
        };
    }

    private static List<PatientFooterItem> NormalizeFooterItems(List<PatientFooterItem>? items)
    {
        return (items ?? new List<PatientFooterItem>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Label) || !string.IsNullOrWhiteSpace(item.Value))
            .Take(12)
            .Select(item => new PatientFooterItem
            {
                Label = TrimOrDefault(item.Label, string.Empty, 80),
                Value = TrimOrDefault(item.Value, string.Empty, 180),
                IconClass = TrimOrDefault(item.IconClass, "fas fa-circle", 80),
                Url = NormalizeLinkUrl(item.Url),
                Highlight = item.Highlight
            })
            .ToList();
    }

    private static List<PatientFooterLink> NormalizeFooterLinks(
        List<PatientFooterLink>? links,
        List<PatientFooterLink> fallback,
        int maxCount)
    {
        return (links ?? fallback)
            .Where(link => !string.IsNullOrWhiteSpace(link.Label) || !string.IsNullOrWhiteSpace(link.Url))
            .Take(maxCount)
            .Select(link => new PatientFooterLink
            {
                Label = TrimOrDefault(link.Label, "Link", 80),
                IconClass = TrimOrDefault(link.IconClass, "fas fa-link", 80),
                Url = NormalizeLinkUrl(link.Url)
            })
            .ToList();
    }

    private static string NormalizeLinkUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "#";
        }

        var url = value.Trim();
        if (url.StartsWith('/') || url.StartsWith('#')
            || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? url
                : "#";
    }

    private static string NormalizeMapUrl(string? value)
    {
        var url = value?.Trim() ?? string.Empty;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? url
            : string.Empty;
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


