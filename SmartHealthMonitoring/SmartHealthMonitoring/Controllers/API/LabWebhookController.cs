using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class LabWebhookController : ControllerBase
    {
        private readonly IMemoryCache _cache;
        private readonly IMinioService _minioService; // Tiêm dịch vụ lưu trữ Object Storage

        public LabWebhookController(IMemoryCache cache, IMinioService minioService)
        {
            _cache = cache;
            _minioService = minioService;
        }

        // POST: api/LabWebhook/ReceiveResult
        [HttpPost("ReceiveResult")]
        public async Task<IActionResult> ReceiveResult([FromBody] LabWebhookPayload payload)
        {
            if (payload == null || payload.PatientId <= 0)
            {
                return BadRequest("Payload không hợp lệ.");
            }

            try
            {
                // XỬ LÝ TẬP TIN ẢNH NẾU ĐƯỢC MÁY XÉT NGHIỆM ĐẨY KÈM DỮ LIỆU BASE64
                if (!string.IsNullOrEmpty(payload.EcgImageBase64))
                {
                    // Loại bỏ tiền tố Data URL nếu có (ví dụ: "data:image/png;base64,")
                    string cleanBase64 = payload.EcgImageBase64.Contains(",")
                        ? payload.EcgImageBase64.Split(',')[1]
                        : payload.EcgImageBase64;

                    // Giải mã chuỗi Base64 thành mảng byte vật lý
                    byte[] imageBytes = Convert.FromBase64String(cleanBase64);

                    using (var stream = new MemoryStream(imageBytes))
                    {
                        // Định nghĩa cấu trúc tên file duy nhất tránh ghi đè
                        string bucketName = "ecg-images";
                        string objectName = $"ecg_{payload.PatientId}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.png";

                        // Tải tệp dữ liệu lên MinIO Server
                        await _minioService.UploadFileAsync(bucketName, objectName, stream, "image/png");

                        // Tạo đường dẫn liên kết bảo mật có thời hạn sử dụng là 7 ngày (10080 phút)
                        string presignedUrl = await _minioService.GetPresignedUrlAsync(bucketName, objectName, 10080);

                        // Gán đường dẫn vào payload để lưu trữ tạm vào bộ nhớ đệm Cache
                        payload.EcgImageUrl = presignedUrl;
                    }
                }

                string cacheKey = $"LabResult_{payload.PatientId}";
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                // Lưu trữ dữ liệu cấu trúc kèm link ảnh vào bộ nhớ đệm MemoryCache
                _cache.Set(cacheKey, payload, cacheEntryOptions);

                return Ok(new { message = $"Đã nhận kết quả xét nghiệm và xử lý tệp ảnh ECG thành công cho PatientId: {payload.PatientId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi xử lý luồng dữ liệu tập tin: " + ex.Message });
            }
        }

        // GET: api/LabWebhook/CheckResult/{patientId}
        [HttpGet("CheckResult/{patientId}")]
        public IActionResult CheckResult(int patientId)
        {
            string cacheKey = $"LabResult_{patientId}";

            if (_cache.TryGetValue(cacheKey, out LabWebhookPayload? result))
            {
                return Ok(result);
            }

            return NotFound(new { message = "Chưa có kết quả từ máy xét nghiệm. Vui lòng đợi thiết bị đẩy dữ liệu." });
        }
    }
}