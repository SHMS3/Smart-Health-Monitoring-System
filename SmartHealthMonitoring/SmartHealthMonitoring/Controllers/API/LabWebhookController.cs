using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Interfaces.Minio;
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

        [HttpPost("ReceiveResult")]
        public async Task<IActionResult> ReceiveResult([FromBody] LabWebhookPayload payload)
        {
            if (payload == null || payload.PatientId <= 0)
            {
                return BadRequest("Payload không hợp lệ.");
            }

            try
            {
                if (!string.IsNullOrEmpty(payload.EcgImageBase64))
                {
                    string cleanBase64 = payload.EcgImageBase64.Contains(",")
                        ? payload.EcgImageBase64.Split(',')[1]
                        : payload.EcgImageBase64;

                    byte[] imageBytes = Convert.FromBase64String(cleanBase64);

                    using (var stream = new MemoryStream(imageBytes))
                    {
                        string bucketName = "ecg-images";
                        string objectName = $"ecg_{payload.PatientId}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.png";

                        await _minioService.UploadFileAsync(bucketName, objectName, stream, "image/png");

                        string presignedUrl = await _minioService.GetPresignedUrlAsync(bucketName, objectName, 10080);

                        payload.EcgImageUrl = presignedUrl;
                    }
                }

                string cacheKey = $"LabResult_{payload.PatientId}";
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                _cache.Set(cacheKey, payload, cacheEntryOptions);

                return Ok(new { message = $"Đã nhận kết quả xét nghiệm và xử lý tệp ảnh ECG thành công cho PatientId: {payload.PatientId}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi xử lý luồng dữ liệu tập tin: " + ex.Message });
            }
        }

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
