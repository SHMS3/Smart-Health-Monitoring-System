using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.ViewModels.Admin;

public class PatientUiSettingsViewModel
{
    public static readonly string[] AllowedLogoIcons =
    {
        "fas fa-heartbeat",
        "fas fa-notes-medical",
        "fas fa-stethoscope",
        "fas fa-user-md",
        "fas fa-hospital",
        "fas fa-shield-heart"
    };

    [Required(ErrorMessage = "Vui l�ng nh?p t�n thuong hi?u.")]
    [MaxLength(80, ErrorMessage = "T�n thuong hi?u kh�ng du?c vu?t qu� 80 k� t?.")]
    public string BrandName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p d�ng m� t? ng?n.")]
    [MaxLength(80, ErrorMessage = "D�ng m� t? kh�ng du?c vu?t qu� 80 k� t?.")]
    public string BrandSubtitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p m� t? trang.")]
    [MaxLength(180, ErrorMessage = "M� t? trang kh�ng du?c vu?t qu� 180 k� t?.")]
    public string MetaDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p g?i � � t�m ki?m.")]
    [MaxLength(120, ErrorMessage = "G?i � t�m ki?m kh�ng du?c vu?t qu� 120 k� t?.")]
    public string SearchPlaceholder { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p nh�n hero.")]
    [MaxLength(100, ErrorMessage = "Nh�n hero kh�ng du?c vu?t qu� 100 k� t?.")]
    public string HomeHeroEyebrow { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p ti�u d? hero.")]
    [MaxLength(120, ErrorMessage = "Ti�u d? hero kh�ng du?c vu?t qu� 120 k� t?.")]
    public string HomeHeroTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p ph?n nh?n c?a ti�u d?.")]
    [MaxLength(120, ErrorMessage = "Ph?n nh?n kh�ng du?c vu?t qu� 120 k� t?.")]
    public string HomeHeroHighlight { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p nh�n g�i.")]
    [MaxLength(80, ErrorMessage = "Nh�n g�i kh�ng du?c vu?t qu� 80 k� t?.")]
    public string HomeHeroPriceTag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p gi�/uu d�i.")]
    [MaxLength(60, ErrorMessage = "Gi�/uu d�i kh�ng du?c vu?t qu� 60 k� t?.")]
    public string HomeHeroPrice { get; set; } = string.Empty;

    [MaxLength(80, ErrorMessage = "H?u t? gi� kh�ng du?c vu?t qu� 80 k� t?.")]
    public string HomeHeroPriceSuffix { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p ghi ch� hero.")]
    [MaxLength(140, ErrorMessage = "Ghi ch� hero kh�ng du?c vu?t qu� 140 k� t?.")]
    public string HomeHeroNote { get; set; } = string.Empty;

    public string HomeHeroImageUrl { get; set; } = string.Empty;

    public IFormFile? HomeHeroImageFile { get; set; }

    [Required(ErrorMessage = "Vui l�ng nh?p nh�n gi?i thi?u.")]
    [MaxLength(80, ErrorMessage = "Nh�n gi?i thi?u kh�ng du?c vu?t qu� 80 k� t?.")]
    public string HomeAboutTag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p ti�u d? gi?i thi?u.")]
    [MaxLength(120, ErrorMessage = "Ti�u d? gi?i thi?u kh�ng du?c vu?t qu� 120 k� t?.")]
    public string HomeAboutTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p m� t? gi?i thi?u.")]
    [MaxLength(500, ErrorMessage = "M� t? gi?i thi?u kh�ng du?c vu?t qu� 500 k� t?.")]
    public string HomeAboutDescription { get; set; } = string.Empty;

    public string HomeAboutImageUrl { get; set; } = string.Empty;

    public IFormFile? HomeAboutImageFile { get; set; }

    [Required(ErrorMessage = "Vui l�ng nh?p ti�u d? CTA.")]
    [MaxLength(120, ErrorMessage = "Ti�u d? CTA kh�ng du?c vu?t qu� 120 k� t?.")]
    public string HomeCtaTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p m� t? CTA.")]
    [MaxLength(220, ErrorMessage = "M� t? CTA kh�ng du?c vu?t qu� 220 k� t?.")]
    public string HomeCtaDescription { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "M�u ch? d?o ph?i c� d?ng #RRGGBB.")]
    public string PrimaryColor { get; set; } = "#1a73c8";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "M�u nh?n d?m ph?i c� d?ng #RRGGBB.")]
    public string PrimaryDarkColor { get; set; } = "#135fa5";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "M�u thanh menu ph?i c� d?ng #RRGGBB.")]
    public string NavigationColor { get; set; } = "#1565c0";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "M�u c?nh b�o ph?i c� d?ng #RRGGBB.")]
    public string AccentColor { get; set; } = "#e53935";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "M�u n?n ph?i c� d?ng #RRGGBB.")]
    public string BackgroundColor { get; set; } = "#f4f7fb";

    [Required(ErrorMessage = "Vui l�ng ch?n bi?u tu?ng logo.")]
    public string LogoIcon { get; set; } = "fas fa-heartbeat";

    [Required(ErrorMessage = "Vui l�ng nh?p s? hotline.")]
    [MaxLength(30, ErrorMessage = "S? hotline kh�ng du?c vu?t qu� 30 k� t?.")]
    public string HotlinePhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p nh�n hotline.")]
    [MaxLength(40, ErrorMessage = "Nh�n hotline kh�ng du?c vu?t qu� 40 k� t?.")]
    public string HotlineLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p email li�n h?.")]
    [EmailAddress(ErrorMessage = "Email li�n h? kh�ng h?p l?.")]
    [MaxLength(120, ErrorMessage = "Email li�n h? kh�ng du?c vu?t qu� 120 k� t?.")]
    public string ContactEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p d?a ch?.")]
    [MaxLength(160, ErrorMessage = "�?a ch? kh�ng du?c vu?t qu� 160 k� t?.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p ph? d? ch�n trang.")]
    [MaxLength(80, ErrorMessage = "Ph? d? ch�n trang kh�ng du?c vu?t qu� 80 k� t?.")]
    public string FooterSubtitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p m� t? ch�n trang.")]
    [MaxLength(260, ErrorMessage = "M� t? ch�n trang kh�ng du?c vu?t qu� 260 k� t?.")]
    public string FooterDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p d�ng ph�p l� ch�n trang.")]
    [MaxLength(180, ErrorMessage = "D�ng ph�p l� kh�ng du?c vu?t qu� 180 k� t?.")]
    public string FooterLicenseText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui l�ng nh?p d�ng cu?i footer.")]
    [MaxLength(180, ErrorMessage = "D�ng cu?i footer kh�ng du?c vu?t qu� 180 k� t?.")]
    public string FooterBottomText { get; set; } = string.Empty;

    public List<PatientFooterEditorItemViewModel> WorkScheduleItems { get; set; } = new();

    public List<PatientFooterEditorItemViewModel> ContactItems { get; set; } = new();

    public bool ShowTopInfoBar { get; set; }

    public bool ShowAiChatbot { get; set; }

    public bool ShowSupportHub { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByAdminName { get; set; }

    public List<SelectListItem> LogoIconOptions { get; set; } = new();

    public static PatientUiSettingsViewModel FromSettings(PatientUiSettings settings)
    {
        return new PatientUiSettingsViewModel
        {
            BrandName = settings.BrandName,
            BrandSubtitle = settings.BrandSubtitle,
            MetaDescription = settings.MetaDescription,
            SearchPlaceholder = settings.SearchPlaceholder,
            HomeHeroEyebrow = settings.HomeHeroEyebrow,
            HomeHeroTitle = settings.HomeHeroTitle,
            HomeHeroHighlight = settings.HomeHeroHighlight,
            HomeHeroPriceTag = settings.HomeHeroPriceTag,
            HomeHeroPrice = settings.HomeHeroPrice,
            HomeHeroPriceSuffix = settings.HomeHeroPriceSuffix,
            HomeHeroNote = settings.HomeHeroNote,
            HomeHeroImageUrl = settings.HomeHeroImageUrl,
            HomeAboutTag = settings.HomeAboutTag,
            HomeAboutTitle = settings.HomeAboutTitle,
            HomeAboutDescription = settings.HomeAboutDescription,
            HomeAboutImageUrl = settings.HomeAboutImageUrl,
            HomeCtaTitle = settings.HomeCtaTitle,
            HomeCtaDescription = settings.HomeCtaDescription,
            PrimaryColor = settings.PrimaryColor,
            PrimaryDarkColor = settings.PrimaryDarkColor,
            NavigationColor = settings.NavigationColor,
            AccentColor = settings.AccentColor,
            BackgroundColor = settings.BackgroundColor,
            LogoIcon = settings.LogoIcon,
            HotlinePhone = settings.HotlinePhone,
            HotlineLabel = settings.HotlineLabel,
            ContactEmail = settings.ContactEmail,
            Address = settings.Address,
            FooterSubtitle = settings.FooterSubtitle,
            FooterDescription = settings.FooterDescription,
            FooterLicenseText = settings.FooterLicenseText,
            FooterBottomText = settings.FooterBottomText,
            WorkScheduleItems = ToEditorItems(settings.FooterSections
                .FirstOrDefault(section => section.DisplayType == PatientFooterSectionDisplayTypes.Schedule)?.Items),
            ContactItems = ToEditorItems(settings.FooterSections
                .FirstOrDefault(section => section.DisplayType == PatientFooterSectionDisplayTypes.Contact)?.Items),
            ShowTopInfoBar = settings.ShowTopInfoBar,
            ShowAiChatbot = settings.ShowAiChatbot,
            ShowSupportHub = settings.ShowSupportHub,
            UpdatedAt = settings.UpdatedAt,
            UpdatedByAdminName = settings.UpdatedByAdminName,
            LogoIconOptions = BuildLogoIconOptions(settings.LogoIcon)
        };
    }

    public void EnsureOptions()
    {
        LogoIconOptions = BuildLogoIconOptions(LogoIcon);
        WorkScheduleItems ??= new List<PatientFooterEditorItemViewModel>();
        ContactItems ??= new List<PatientFooterEditorItemViewModel>();
    }

    private static List<PatientFooterEditorItemViewModel> ToEditorItems(List<PatientFooterItem>? items)
    {
        return (items ?? new List<PatientFooterItem>())
            .Select(item => new PatientFooterEditorItemViewModel
            {
                Label = item.Label,
                Value = item.Value,
                Kind = InferItemKind(item),
                Highlight = item.Highlight
            })
            .ToList();
    }

    private static string InferItemKind(PatientFooterItem item)
    {
        if (item.Url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            return PatientFooterItemKinds.Phone;
        if (item.Url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return PatientFooterItemKinds.Email;
        if (item.IconClass.Contains("map-marker", StringComparison.OrdinalIgnoreCase))
            return PatientFooterItemKinds.Address;
        if (item.IconClass.Contains("globe", StringComparison.OrdinalIgnoreCase))
            return PatientFooterItemKinds.Website;
        if (item.IconClass.Contains("phone", StringComparison.OrdinalIgnoreCase))
            return PatientFooterItemKinds.Phone;
        if (item.Highlight && item.IconClass.Contains("circle", StringComparison.OrdinalIgnoreCase))
            return PatientFooterItemKinds.Status;
        return PatientFooterItemKinds.Text;
    }

    private static List<SelectListItem> BuildLogoIconOptions(string selectedIcon)
    {
        var labels = new Dictionary<string, string>
        {
            ["fas fa-heartbeat"] = "Nh?p tim",
            ["fas fa-notes-medical"] = "S? y t?",
            ["fas fa-stethoscope"] = "?ng nghe",
            ["fas fa-user-md"] = "B�c si",
            ["fas fa-hospital"] = "B?nh vi?n",
            ["fas fa-shield-heart"] = "B?o v? tim"
        };

        return AllowedLogoIcons
            .Select(icon => new SelectListItem
            {
                Value = icon,
                Text = labels[icon],
                Selected = icon == selectedIcon
            })
            .ToList();
    }
}

public static class PatientFooterItemKinds
{
    public const string Text = "text";
    public const string Address = "address";
    public const string Phone = "phone";
    public const string Email = "email";
    public const string Website = "website";
    public const string Status = "status";
}

public class PatientFooterEditorItemViewModel
{
    [MaxLength(80, ErrorMessage = "Nh�n kh�ng du?c vu?t qu� 80 k� t?.")]
    public string Label { get; set; } = string.Empty;

    [MaxLength(180, ErrorMessage = "N?i dung kh�ng du?c vu?t qu� 180 k� t?.")]
    public string Value { get; set; } = string.Empty;

    public string Kind { get; set; } = PatientFooterItemKinds.Text;

    public bool Highlight { get; set; }
}
