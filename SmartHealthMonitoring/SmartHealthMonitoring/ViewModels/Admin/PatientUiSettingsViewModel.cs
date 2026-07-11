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

    public static readonly string[] AllowedFooterIcons =
    {
        "fas fa-clock",
        "fas fa-map-marker-alt",
        "fas fa-map",
        "fas fa-phone-alt",
        "fas fa-envelope",
        "fas fa-globe",
        "fas fa-calendar-days",
        "fas fa-circle-info",
        "fas fa-circle",
        "fas fa-link",
        "fab fa-facebook-f",
        "fab fa-instagram",
        "fab fa-youtube",
        "fab fa-tiktok",
        "fab fa-linkedin-in"
    };

    [Required(ErrorMessage = "Vui lòng nhập tên thương hiệu.")]
    [MaxLength(80, ErrorMessage = "Tên thương hiệu không được vượt quá 80 ký tự.")]
    public string BrandName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập dòng mô tả ngắn.")]
    [MaxLength(80, ErrorMessage = "Dòng mô tả không được vượt quá 80 ký tự.")]
    public string BrandSubtitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả trang.")]
    [MaxLength(180, ErrorMessage = "Mô tả trang không được vượt quá 180 ký tự.")]
    public string MetaDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập gợi ý ô tìm kiếm.")]
    [MaxLength(120, ErrorMessage = "Gợi ý tìm kiếm không được vượt quá 120 ký tự.")]
    public string SearchPlaceholder { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nhãn hero.")]
    [MaxLength(100, ErrorMessage = "Nhãn hero không được vượt quá 100 ký tự.")]
    public string HomeHeroEyebrow { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề hero.")]
    [MaxLength(120, ErrorMessage = "Tiêu đề hero không được vượt quá 120 ký tự.")]
    public string HomeHeroTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập phần nhấn của tiêu đề.")]
    [MaxLength(120, ErrorMessage = "Phần nhấn không được vượt quá 120 ký tự.")]
    public string HomeHeroHighlight { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nhãn gói.")]
    [MaxLength(80, ErrorMessage = "Nhãn gói không được vượt quá 80 ký tự.")]
    public string HomeHeroPriceTag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập giá/ưu đãi.")]
    [MaxLength(60, ErrorMessage = "Giá/ưu đãi không được vượt quá 60 ký tự.")]
    public string HomeHeroPrice { get; set; } = string.Empty;

    [MaxLength(80, ErrorMessage = "Hậu tố giá không được vượt quá 80 ký tự.")]
    public string HomeHeroPriceSuffix { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập ghi chú hero.")]
    [MaxLength(140, ErrorMessage = "Ghi chú hero không được vượt quá 140 ký tự.")]
    public string HomeHeroNote { get; set; } = string.Empty;

    public string HomeHeroImageUrl { get; set; } = string.Empty;

    public IFormFile? HomeHeroImageFile { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nhãn giới thiệu.")]
    [MaxLength(80, ErrorMessage = "Nhãn giới thiệu không được vượt quá 80 ký tự.")]
    public string HomeAboutTag { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề giới thiệu.")]
    [MaxLength(120, ErrorMessage = "Tiêu đề giới thiệu không được vượt quá 120 ký tự.")]
    public string HomeAboutTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả giới thiệu.")]
    [MaxLength(500, ErrorMessage = "Mô tả giới thiệu không được vượt quá 500 ký tự.")]
    public string HomeAboutDescription { get; set; } = string.Empty;

    public string HomeAboutImageUrl { get; set; } = string.Empty;

    public IFormFile? HomeAboutImageFile { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề CTA.")]
    [MaxLength(120, ErrorMessage = "Tiêu đề CTA không được vượt quá 120 ký tự.")]
    public string HomeCtaTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả CTA.")]
    [MaxLength(220, ErrorMessage = "Mô tả CTA không được vượt quá 220 ký tự.")]
    public string HomeCtaDescription { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Màu chủ đạo phải có dạng #RRGGBB.")]
    public string PrimaryColor { get; set; } = "#1a73c8";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Màu nhấn đậm phải có dạng #RRGGBB.")]
    public string PrimaryDarkColor { get; set; } = "#135fa5";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Màu thanh menu phải có dạng #RRGGBB.")]
    public string NavigationColor { get; set; } = "#1565c0";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Màu cảnh báo phải có dạng #RRGGBB.")]
    public string AccentColor { get; set; } = "#e53935";

    [Required]
    [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Màu nền phải có dạng #RRGGBB.")]
    public string BackgroundColor { get; set; } = "#f4f7fb";

    [Required(ErrorMessage = "Vui lòng chọn biểu tượng logo.")]
    public string LogoIcon { get; set; } = "fas fa-heartbeat";

    [Required(ErrorMessage = "Vui lòng nhập số hotline.")]
    [MaxLength(30, ErrorMessage = "Số hotline không được vượt quá 30 ký tự.")]
    public string HotlinePhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nhãn hotline.")]
    [MaxLength(40, ErrorMessage = "Nhãn hotline không được vượt quá 40 ký tự.")]
    public string HotlineLabel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email liên hệ.")]
    [EmailAddress(ErrorMessage = "Email liên hệ không hợp lệ.")]
    [MaxLength(120, ErrorMessage = "Email liên hệ không được vượt quá 120 ký tự.")]
    public string ContactEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [MaxLength(160, ErrorMessage = "Địa chỉ không được vượt quá 160 ký tự.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập phụ đề chân trang.")]
    [MaxLength(80, ErrorMessage = "Phụ đề chân trang không được vượt quá 80 ký tự.")]
    public string FooterSubtitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chân trang.")]
    [MaxLength(260, ErrorMessage = "Mô tả chân trang không được vượt quá 260 ký tự.")]
    public string FooterDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập dòng pháp lý chân trang.")]
    [MaxLength(180, ErrorMessage = "Dòng pháp lý không được vượt quá 180 ký tự.")]
    public string FooterLicenseText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập dòng cuối footer.")]
    [MaxLength(180, ErrorMessage = "Dòng cuối footer không được vượt quá 180 ký tự.")]
    public string FooterBottomText { get; set; } = string.Empty;

    public List<PatientFooterLinkViewModel> FooterSocialLinks { get; set; } = new();

    public List<PatientFooterSectionViewModel> FooterSections { get; set; } = new();

    public List<PatientFooterLinkViewModel> FooterBottomLinks { get; set; } = new();

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
            FooterSocialLinks = settings.FooterSocialLinks.Select(PatientFooterLinkViewModel.FromModel).ToList(),
            FooterSections = settings.FooterSections.Select(PatientFooterSectionViewModel.FromModel).ToList(),
            FooterBottomLinks = settings.FooterBottomLinks.Select(PatientFooterLinkViewModel.FromModel).ToList(),
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
        FooterSocialLinks ??= new();
        FooterSections ??= new();
        FooterBottomLinks ??= new();

        foreach (var section in FooterSections)
        {
            section.Items ??= new();
        }
    }

    private static List<SelectListItem> BuildLogoIconOptions(string selectedIcon)
    {
        var labels = new Dictionary<string, string>
        {
            ["fas fa-heartbeat"] = "Nhịp tim",
            ["fas fa-notes-medical"] = "Sổ y tế",
            ["fas fa-stethoscope"] = "Ống nghe",
            ["fas fa-user-md"] = "Bác sĩ",
            ["fas fa-hospital"] = "Bệnh viện",
            ["fas fa-shield-heart"] = "Bảo vệ tim"
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

public class PatientFooterSectionViewModel
{
    [MaxLength(80)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(40)]
    public string IconClass { get; set; } = "fas fa-circle-info";

    [MaxLength(20)]
    public string DisplayType { get; set; } = PatientFooterSectionDisplayTypes.Contact;

    [MaxLength(700)]
    public string MapEmbedUrl { get; set; } = string.Empty;

    public List<PatientFooterItemViewModel> Items { get; set; } = new();

    public static PatientFooterSectionViewModel FromModel(PatientFooterSection section)
    {
        return new PatientFooterSectionViewModel
        {
            Title = section.Title,
            IconClass = section.IconClass,
            DisplayType = section.DisplayType,
            MapEmbedUrl = section.MapEmbedUrl,
            Items = section.Items.Select(PatientFooterItemViewModel.FromModel).ToList()
        };
    }
}

public class PatientFooterItemViewModel
{
    [MaxLength(80)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(180)]
    public string Value { get; set; } = string.Empty;

    [MaxLength(40)]
    public string IconClass { get; set; } = "fas fa-circle";

    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    public bool Highlight { get; set; }

    public static PatientFooterItemViewModel FromModel(PatientFooterItem item)
    {
        return new PatientFooterItemViewModel
        {
            Label = item.Label,
            Value = item.Value,
            IconClass = item.IconClass,
            Url = item.Url,
            Highlight = item.Highlight
        };
    }
}

public class PatientFooterLinkViewModel
{
    [MaxLength(80)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(40)]
    public string IconClass { get; set; } = "fas fa-link";

    [MaxLength(500)]
    public string Url { get; set; } = "#";

    public static PatientFooterLinkViewModel FromModel(PatientFooterLink link)
    {
        return new PatientFooterLinkViewModel
        {
            Label = link.Label,
            IconClass = link.IconClass,
            Url = link.Url
        };
    }
}
