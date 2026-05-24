using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestEmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public TestEmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        // Dùng GET để có thể dễ dàng test thẳng bằng trình duyệt web
        [HttpGet("send-warning")]
        public async Task<IActionResult> SendWarningEmail([FromQuery] string toEmail)
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                return BadRequest("Vui lòng cung cấp tham số toEmail. Ví dụ: /api/testemail/send-warning?toEmail=nguyenvana@gmail.com");
            }

            // Dữ liệu mô phỏng bệnh nhân bị nhịp tim cao
            var replacements = new Dictionary<string, string>
            {
                { "{{PatientName}}", "Người Dùng Mẫu" },
                { "{{Timestamp}}", System.DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") },
                { "{{HeartRate}}", "115" },
                { "{{BloodPressure}}", "140/90" },
                { "{{Advice}}", "Nhịp tim của bạn đang khá cao lúc nghỉ ngơi. Vui lòng ngồi nghỉ, uống một chút nước ấm và đo lại sau 15 phút. Nếu vẫn không giảm, hãy liên hệ ngay với bác sĩ chuyên khoa !" }
            };

            // Lấy nội dung HTML từ file template
            string htmlContent = _emailService.GetHtmlContentFromFile("WarningAlertTemplate.html", replacements);

            if (string.IsNullOrEmpty(htmlContent))
            {
                return StatusCode(500, "Không thể đọc nội dung file template HTML. Hãy chắc chắn file WarningAlertTemplate.html nằm đúng trong wwwroot/templates/emails");
            }

            // Thực thi gửi email
            await _emailService.SendEmailAsync(toEmail, "CẢNH BÁO SỨC KHỎE TỪ SMART HEALTH", htmlContent);

            return Ok(new { message = $"Đã gửi báo cáo thử nghiệm thành công tới email: {toEmail}. Hãy kiểm tra hòm thư của bạn !" });
        }
    }
}
