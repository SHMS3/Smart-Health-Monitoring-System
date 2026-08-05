using System.Text.RegularExpressions;
using System.Text.Json;
using Tesseract;
using Microsoft.AspNetCore.Hosting;

using SmartHealthMonitoring.Interfaces.QR;

namespace SmartHealthMonitoring.Services.QR
{
    public class LocalOcrService : ILocalOcrService
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

                    var citizenId = Regex.Match(text, @"\b\d{12}\b").Value;
                    
                    var dateOfBirth = "";
                    var sexDisplay = "";
                    var fullName = "";
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

