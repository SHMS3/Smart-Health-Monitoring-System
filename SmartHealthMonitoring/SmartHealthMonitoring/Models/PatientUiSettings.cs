namespace SmartHealthMonitoring.Models;

public class PatientUiSettings
{
    public string BrandName { get; set; } = "SmartHealth";

    public string BrandSubtitle { get; set; } = "Tim Mạch";

    public string MetaDescription { get; set; } = "SmartHealth Tim mạch - Hệ thống theo dõi sức khỏe tim mạch thông minh, cảnh báo sớm bằng AI.";

    public string SearchPlaceholder { get; set; } = "Tìm kiếm dịch vụ, thông tin...";

    public string HomeHeroEyebrow { get; set; } = "Ưu đãi dành riêng cho bệnh nhân";

    public string HomeHeroTitle { get; set; } = "Theo dõi Sức khỏe Tim Mạch";

    public string HomeHeroHighlight { get; set; } = "Thông minh cùng AI";

    public string HomeHeroPriceTag { get; set; } = "Gói theo dõi hằng ngày";

    public string HomeHeroPrice { get; set; } = "Miễn phí";

    public string HomeHeroPriceSuffix { get; set; } = " khi đăng ký";

    public string HomeHeroNote { get; set; } = "Được theo dõi bởi bác sĩ chuyên khoa tim mạch";

    public string HomeHeroImageUrl { get; set; } = "/images/banner.jpg";

    public string HomeAboutTag { get; set; } = "Giới thiệu về chúng tôi";

    public string HomeAboutTitle { get; set; } = "Hệ thống SmartHealth Tim Mạch";

    public string HomeAboutDescription { get; set; } = "SmartHealth được thành lập với sứ mệnh ứng dụng công nghệ AI trong theo dõi và cảnh báo sức khỏe tim mạch. Đội ngũ bác sĩ chuyên khoa tim mạch kết hợp hệ thống phân tích dữ liệu thông minh giúp phát hiện sớm các nguy cơ và đưa ra can thiệp kịp thời.";

    public string HomeAboutImageUrl { get; set; } = "/images/about-banner.png";

    public string HomeCtaTitle { get; set; } = "Bắt đầu theo dõi tim mạch ngay hôm nay";

    public string HomeCtaDescription { get; set; } = "Đăng ký miễn phí và nhận cảnh báo nguy cơ tim mạch từ hệ thống AI 24/7.";

    public string PrimaryColor { get; set; } = "#1a73c8";

    public string PrimaryDarkColor { get; set; } = "#135fa5";

    public string NavigationColor { get; set; } = "#1565c0";

    public string AccentColor { get; set; } = "#e53935";

    public string BackgroundColor { get; set; } = "#f4f7fb";

    public string LogoIcon { get; set; } = "fas fa-heartbeat";

    public string HotlinePhone { get; set; } = "0999 999 999";

    public string HotlineLabel { get; set; } = "Hotline 24/7";

    public string ContactEmail { get; set; } = "contact@smarthealth.vn";

    public string Address { get; set; } = "Đại học FPT, TP. Hồ Chí Minh";

    public string FooterSubtitle { get; set; } = "Theo dõi Tim mạch AI";

    public string FooterDescription { get; set; } = "Hệ thống theo dõi sức khỏe tim mạch thông minh. Phát hiện sớm nguy cơ và nhận cảnh báo kịp thời từ AI.";

    public string FooterLicenseText { get; set; } = "Giấy phép số 47/GP-TTĐT, ngày 20 tháng 01 năm 2017";

    public string FooterBottomText { get; set; } = "© 2026 SmartHealth. Bảo lưu mọi quyền.";

    public List<PatientFooterLink> FooterSocialLinks { get; set; } = CreateDefaultFooterSocialLinks();

    public List<PatientFooterSection> FooterSections { get; set; } = CreateDefaultFooterSections();

    public List<PatientFooterLink> FooterBottomLinks { get; set; } = CreateDefaultFooterBottomLinks();

    public bool ShowTopInfoBar { get; set; } = true;

    public bool ShowAiChatbot { get; set; } = true;

    public bool ShowSupportHub { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

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
            new() { Label = "Điều khoản", Url = "#" },
            new() { Label = "Bảo mật", Url = "#" }
        };
    }

    private static List<PatientFooterSection> CreateDefaultFooterSections()
    {
        return new List<PatientFooterSection>
        {
            new()
            {
                Title = "Lịch làm việc",
                IconClass = "fas fa-clock",
                DisplayType = PatientFooterSectionDisplayTypes.Schedule,
                Items = new List<PatientFooterItem>
                {
                    new() { Label = "Thứ 2 - Thứ 7", Value = "08:00 - 17:00" },
                    new() { Label = "Chuyên khoa Tim mạch", Value = "08:00 - 12:00" },
                    new() { Label = "Thứ 2 - Thứ 6", Value = "17:00 - 20:00" },
                    new() { Label = "Chủ nhật", Value = "Nghỉ", Highlight = true },
                    new() { Label = "Hotline 24/7", Value = "0999 999 999", IconClass = "fas fa-phone-alt", Highlight = true }
                }
            },
            new()
            {
                Title = "Liên hệ",
                IconClass = "fas fa-map-marker-alt",
                DisplayType = PatientFooterSectionDisplayTypes.Contact,
                Items = new List<PatientFooterItem>
                {
                    new() { Label = "Địa chỉ", Value = "Đại học FPT, TP. Hồ Chí Minh", IconClass = "fas fa-map-marker-alt" },
                    new() { Label = "Hotline", Value = "0999 999 999 - Hotline 24/7", IconClass = "fas fa-phone-alt", Url = "tel:0999999999" },
                    new() { Label = "Email", Value = "contact@smarthealth.vn", IconClass = "fas fa-envelope", Url = "mailto:contact@smarthealth.vn" },
                    new() { Label = "Website", Value = "www.smarthealth.vn", IconClass = "fas fa-globe", Url = "#" },
                    new() { Label = "Trạng thái", Value = "Hệ thống đang hoạt động ổn định", IconClass = "fas fa-circle", Highlight = true }
                }
            },
            new()
            {
                Title = "Bản đồ",
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
