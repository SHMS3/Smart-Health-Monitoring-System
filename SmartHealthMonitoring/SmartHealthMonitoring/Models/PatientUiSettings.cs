namespace SmartHealthMonitoring.Models;

public class PatientUiSettings
{
    public string BrandName { get; set; } = "SmartHealth";

    public string BrandSubtitle { get; set; } = "Tim M?ch";

    public string MetaDescription { get; set; } = "SmartHealth Tim m?ch - H? th?ng theo d�i s?c kh?e tim m?ch th�ng minh, c?nh b�o s?m b?ng AI.";

    public string SearchPlaceholder { get; set; } = "T�m ki?m d?ch v?, th�ng tin...";

    public string HomeHeroEyebrow { get; set; } = "Uu d�i d�nh ri�ng cho b?nh nh�n";

    public string HomeHeroTitle { get; set; } = "Theo d�i S?c kh?e Tim M?ch";

    public string HomeHeroHighlight { get; set; } = "Th�ng minh c�ng AI";

    public string HomeHeroPriceTag { get; set; } = "G�i theo d�i h?ng ng�y";

    public string HomeHeroPrice { get; set; } = "Mi?n ph�";

    public string HomeHeroPriceSuffix { get; set; } = " khi dang k�";

    public string HomeHeroNote { get; set; } = "�u?c theo d�i b?i b�c si chuy�n khoa tim m?ch";

    public string HomeHeroImageUrl { get; set; } = "/images/banner.jpg";

    public string HomeAboutTag { get; set; } = "Gi?i thi?u v? ch�ng t�i";

    public string HomeAboutTitle { get; set; } = "H? th?ng SmartHealth Tim M?ch";

    public string HomeAboutDescription { get; set; } = "SmartHealth du?c th�nh l?p v?i s? m?nh ?ng d?ng c�ng ngh? AI trong theo d�i v� c?nh b�o s?c kh?e tim m?ch. �?i ngu b�c si chuy�n khoa tim m?ch k?t h?p h? th?ng ph�n t�ch d? li?u th�ng minh gi�p ph�t hi?n s?m c�c nguy co v� dua ra can thi?p k?p th?i.";

    public string HomeAboutImageUrl { get; set; } = "/images/about-banner.png";

    public string HomeCtaTitle { get; set; } = "B?t d?u theo d�i tim m?ch ngay h�m nay";

    public string HomeCtaDescription { get; set; } = "�ang k� mi?n ph� v� nh?n c?nh b�o nguy co tim m?ch t? h? th?ng AI 24/7.";

    public string PrimaryColor { get; set; } = "#1a73c8";

    public string PrimaryDarkColor { get; set; } = "#135fa5";

    public string NavigationColor { get; set; } = "#1565c0";

    public string AccentColor { get; set; } = "#e53935";

    public string BackgroundColor { get; set; } = "#f4f7fb";

    public string LogoIcon { get; set; } = "fas fa-heartbeat";

    public string HotlinePhone { get; set; } = "0999 999 999";

    public string HotlineLabel { get; set; } = "Hotline 24/7";

    public string ContactEmail { get; set; } = "contact@smarthealth.vn";

    public string Address { get; set; } = "�?i h?c FPT, TP. H? Ch� Minh";

    public string FooterSubtitle { get; set; } = "Theo d�i Tim m?ch AI";

    public string FooterDescription { get; set; } = "H? th?ng theo d�i s?c kh?e tim m?ch th�ng minh. Ph�t hi?n s?m nguy co v� nh?n c?nh b�o k?p th?i t? AI.";

    public string FooterLicenseText { get; set; } = "Gi?y ph�p s? 47/GP-TT�T, ng�y 20 th�ng 01 nam 2017";

    public string FooterBottomText { get; set; } = "� 2026 SmartHealth. B?o luu m?i quy?n.";

    public List<PatientFooterLink> FooterSocialLinks { get; set; } = CreateDefaultFooterSocialLinks();

    public List<PatientFooterSection> FooterSections { get; set; } = CreateDefaultFooterSections();

    public List<PatientFooterLink> FooterBottomLinks { get; set; } = CreateDefaultFooterBottomLinks();

    public bool ShowTopInfoBar { get; set; } = true;

    public bool ShowAiChatbot { get; set; } = true;

    public bool ShowSupportHub { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

    public string? UpdatedByAdminName { get; set; }

    private static List<PatientFooterLink> CreateDefaultFooterSocialLinks()
    {
        return new List<PatientFooterLink>
        {
            new() { Label = "Facebook", IconClass = "fab fa-facebook-f", Url = "#" },
            new() { Label = "Instagram", IconClass = "fab fa-instagram", Url = "#" },
            new() { Label = "YouTube", IconClass = "fab fa-youtube", Url = "#" },
            new() { Label = "Email", IconClass = "fas fa-envelope", Url = "mailto:contact@smarthealth.vn" }
        };
    }

    private static List<PatientFooterLink> CreateDefaultFooterBottomLinks()
    {
        return new List<PatientFooterLink>
        {
            new() { Label = "�i?u kho?n", Url = "#" },
            new() { Label = "B?o m?t", Url = "#" }
        };
    }

    private static List<PatientFooterSection> CreateDefaultFooterSections()
    {
        return new List<PatientFooterSection>
        {
            new()
            {
                Title = "L?ch l�m vi?c",
                IconClass = "fas fa-clock",
                DisplayType = PatientFooterSectionDisplayTypes.Schedule,
                Items = new List<PatientFooterItem>
                {
                    new() { Label = "Th? 2 - Th? 7", Value = "08:00 - 17:00" },
                    new() { Label = "Chuy�n khoa Tim m?ch", Value = "08:00 - 12:00" },
                    new() { Label = "Th? 2 - Th? 6", Value = "17:00 - 20:00" },
                    new() { Label = "Ch? nh?t", Value = "Ngh?", Highlight = true },
                    new() { Label = "Hotline 24/7", Value = "0999 999 999", IconClass = "fas fa-phone-alt", Highlight = true }
                }
            },
            new()
            {
                Title = "Li�n h?",
                IconClass = "fas fa-map-marker-alt",
                DisplayType = PatientFooterSectionDisplayTypes.Contact,
                Items = new List<PatientFooterItem>
                {
                    new() { Label = "�?a ch?", Value = "�?i h?c FPT, TP. H? Ch� Minh", IconClass = "fas fa-map-marker-alt" },
                    new() { Label = "Hotline", Value = "0999 999 999 - Hotline 24/7", IconClass = "fas fa-phone-alt", Url = "tel:0999999999" },
                    new() { Label = "Email", Value = "contact@smarthealth.vn", IconClass = "fas fa-envelope", Url = "mailto:contact@smarthealth.vn" },
                    new() { Label = "Website", Value = "www.smarthealth.vn", IconClass = "fas fa-globe", Url = "#" },
                    new() { Label = "Tr?ng th�i", Value = "H? th?ng dang ho?t d?ng ?n d?nh", IconClass = "fas fa-circle", Highlight = true }
                }
            },
            new()
            {
                Title = "B?n d?",
                IconClass = "fas fa-map",
                DisplayType = PatientFooterSectionDisplayTypes.Map,
                MapEmbedUrl = "https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3919.4482!2d106.8!3d10.85!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x0%3A0x0!2zMTDCsDUxJzAwLjAiTiAxMDbCsDQ4JzAwLjAiRQ!5e0!3m2!1svi!2svn!4v1234567890"
            }
        };
    }
}

public static class PatientFooterSectionDisplayTypes
{
    public const string Schedule = "schedule";
    public const string Contact = "contact";
    public const string Map = "map";
}

public class PatientFooterSection
{
    public string Title { get; set; } = string.Empty;

    public string IconClass { get; set; } = "fas fa-circle";

    public string DisplayType { get; set; } = PatientFooterSectionDisplayTypes.Contact;

    public string MapEmbedUrl { get; set; } = string.Empty;

    public List<PatientFooterItem> Items { get; set; } = new();
}

public class PatientFooterItem
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string IconClass { get; set; } = "fas fa-circle";

    public string Url { get; set; } = string.Empty;

    public bool Highlight { get; set; }
}

public class PatientFooterLink
{
    public string Label { get; set; } = string.Empty;

    public string IconClass { get; set; } = "fas fa-link";

    public string Url { get; set; } = "#";
}
