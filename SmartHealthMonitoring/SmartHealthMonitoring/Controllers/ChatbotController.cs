using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly ChatbotService _chatbotService;

        public ChatbotController(ChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        // THÊM ACTION NÀY ĐỂ NHẬN API TỪ JAVASCRIPT
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Tin nhắn trống");

            try
            {
                // Lấy ID người dùng đang đăng nhập
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // Gọi tới Service chứa Gemini
                var aiResponse = await _chatbotService.SendMessageAsync(userId, request.Message);

                // Trả về JSON cho Javascript hiển thị
                return Json(new { success = true, reply = aiResponse });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    

        [HttpGet]
        public async Task<IActionResult> History(DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                int pageSize = 4; 

                var result = await _chatbotService.GetHistoryAsync(userId, fromDate, toDate, page, pageSize);

                // Giữ lại giá trị bộ lọc trên View
                ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
                ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

                return View(result);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(new PagedResult<ChatbotSession>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int sessionId)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var session = await _chatbotService.GetConversationAsync(sessionId);

                if (session == null) return NotFound();

                return View(session);
            }
            catch (Exception)
            {
                return RedirectToAction("History");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int sessionId)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var success = await _chatbotService.DeleteConversationAsync(sessionId, userId);

                if (success)
                    TempData["SuccessMessage"] = "Đã xóa cuộc trò chuyện thành công.";
                else
                    TempData["ErrorMessage"] = "Không thể xóa cuộc trò chuyện này.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
            }

            return RedirectToAction("History");
        }
    }

    // Class phụ để hứng dữ liệu JSON
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
