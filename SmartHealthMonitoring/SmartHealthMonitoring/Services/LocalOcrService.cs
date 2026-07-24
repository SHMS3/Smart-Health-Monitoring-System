using System.Text.RegularExpressions;
using System.Text.Json;
using Tesseract;
using Microsoft.AspNetCore.Hosting;

namespace SmartHealthMonitoring.Services
{
    public class LocalOcrService
    {
        private readonly string _tessDataPath;
        private readonly ILogger<LocalOcrService> _logger;

        public LocalOcrService(IWebHostEnvironment env, ILogger<LocalOcrService> logger)
        {
            _tessDataPath = Path.Combine(env.ContentRootPath, "tessdata");
            _logger = logger;
        }

        public async Task<string> ScanCitizenIdAsync(byte[] imageBytes)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_tessDataPath))
                    {
                        throw new DirectoryNotFoundException($"Không tìm thấy thư mục tessdata tại: {_tessDataPath}");
                    }

                    using var engine = new TesseractEngine(_tessDataPath, "vie", EngineMode.Default);
                    using var img = Pix.LoadFromMemory(imageBytes);
                    using var page = engine.Process(img);
                    
                    var text = page.GetText();
                    _logger.LogInformation("Raw Tesseract Text: \n{Text}", text);

                    // 1. Trích xuất CCCD (12 số)
                    var citizenId = Regex.Match(text, @"\b\d{12}\b").Value;
                    
                    // 2. Trích xuất Ngày sinh (định dạng dd/MM/yyyy chuyển sang yyyy-MM-dd)
                    // Ràng buộc năm sinh phải bắt đầu bằng 19 hoặc 20 để tránh nhiễu
                    var dobMatch = Regex.Match(text, @"\b(\d{2})[/-](\d{2})[/-]((?:19|20)\d{2})\b");
                    var dateOfBirth = dobMatch.Success ? $"{dobMatch.Groups[3].Value}-{dobMatch.Groups[2].Value}-{dobMatch.Groups[1].Value}" : "";

                    // 3. Trích xuất Giới tính
                    var sexDisplay = "";
                    if (text.Contains("Nam", StringComparison.OrdinalIgnoreCase))
                        sexDisplay = "Nam";
                    else if (text.Contains("Nữ", StringComparison.OrdinalIgnoreCase) || text.Contains("Nu", StringComparison.OrdinalIgnoreCase))
                        sexDisplay = "Nữ";

                    // 4. Trích xuất Họ Tên (Dòng toàn chữ in hoa, tối thiểu 2 từ, khoảng 5 ký tự trở lên)
                    var fullName = "";
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        // Nếu dòng viết HOA toàn bộ và CHỈ chứa chữ cái tiếng Việt/khoảng trắng (không chứa số, dấu câu)
                        if (trimmed.Length > 5 && 
                            trimmed == trimmed.ToUpper() && 
                            Regex.IsMatch(trimmed, @"^[A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯĂẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼỀỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴÝỶỸ\s]+$") &&
                            trimmed.Split(' ').Length >= 2)
                        {
                            // Tránh các từ khóa cố định trên thẻ CCCD
                            if (!trimmed.Contains("CỘNG HÒA") && 
                                !trimmed.Contains("ĐỘC LẬP") && 
                                !trimmed.Contains("CĂN CƯỚC"))
                            {
                                fullName = trimmed;
                                break;
                            }
                        }
                    }

                    // Tesseract rất khó trích xuất chính xác địa chỉ thường trú nếu không có cấu trúc cố định
                    var address = "";

                    var result = new
                    {
                        citizenId,
                        fullName,
                        dateOfBirth,
                        sexDisplay,
                        address
                    };

                    return JsonSerializer.Serialize(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý ảnh CCCD cục bộ bằng Tesseract.");
                    throw new Exception("Lỗi khi đọc ảnh CCCD: " + ex.Message);
                }
            });
        }
    }
}
