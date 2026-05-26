using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers.API
{
        [Route("api/[controller]")]
        [ApiController]
        public class LabWebhookController : ControllerBase
        {
            private readonly IMemoryCache _cache;

            public LabWebhookController(IMemoryCache cache)
            {
                _cache = cache;
            }

            // POST: api/LabWebhook/ReceiveResult
            // Máy xét nghiệm (Postman) gọi hàm này để đẩy dữ liệu
            [HttpPost("ReceiveResult")]
            public IActionResult ReceiveResult([FromBody] LabWebhookPayload payload)
            {
                if (payload == null || payload.PatientId <= 0)
                {
                    return BadRequest("Payload không hợp lệ.");
                }

                string cacheKey = $"LabResult_{payload.PatientId}";

                // Cấu hình thời gian sống của Cache là 30 phút
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                // Lưu dữ liệu vào MemoryCache
                _cache.Set(cacheKey, payload, cacheEntryOptions);

                return Ok(new { message = $"Đã nhận và lưu tạm kết quả xét nghiệm cho PatientId: {payload.PatientId}" });
            }

            // GET: api/LabWebhook/CheckResult/{patientId}
            // Giao diện (JS) của bác sĩ gọi hàm này để lấy dữ liệu
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

