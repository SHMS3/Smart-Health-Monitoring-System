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

    public bool ShowTopInfoBar { get; set; } = true;

    public bool ShowAiChatbot { get; set; } = true;

    public bool ShowSupportHub { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedByAdminName { get; set; }
}
