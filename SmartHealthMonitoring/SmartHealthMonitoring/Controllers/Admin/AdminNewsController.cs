using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.Interfaces.AI;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminNewsController : Controller
    {
        private readonly IAdminNewsService _newsService;
        private readonly IGeminiService _gemini;
        private readonly IAdminStatisticsService _statsService;
        private readonly IWebHostEnvironment _env;

        public AdminNewsController(
            IAdminNewsService newsService,
            IGeminiService gemini,
            IAdminStatisticsService statsService,
            IWebHostEnvironment env)
        {
            _newsService = newsService;
            _gemini = gemini;
            _statsService = statsService;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var news = await _newsService.GetAllAsync(status);
            ViewBag.CurrentStatus = status ?? "all";
            return View(news);
        }

        [HttpGet]
        public IActionResult Create(string? source = null)
        {
            var model = new HealthNewsPost
            {
                Title   = HttpContext.Session.GetString("AiNewsTitle")   ?? string.Empty,
                Summary = HttpContext.Session.GetString("AiNewsSummary") ?? string.Empty,
                Content = HttpContext.Session.GetString("AiNewsContent") ?? string.Empty,
                Source  = HttpContext.Session.GetString("AiNewsSource")  ?? source ?? "Manual",
                Status  = "Draft"
            };
            HttpContext.Session.Remove("AiNewsTitle");
            HttpContext.Session.Remove("AiNewsSummary");
            HttpContext.Session.Remove("AiNewsContent");
            HttpContext.Session.Remove("AiNewsSource");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HealthNewsPost model, string action, IFormFile? uploadImage)
        {
            ModelState.Remove("AuthorName");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("Status");

            if (!ModelState.IsValid)
                return View(model);

            var validation = ValidateImage(uploadImage);
            if (!validation.success)
            {
                ModelState.AddModelError("", validation.message);
                return View(model);
            }

            var adminName = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
            var createdModel = await _newsService.CreateAsync(model, adminName, action, _env.WebRootPath, uploadImage);

            TempData["Success"] = createdModel.Status == "Published"
                ? "✅ Bài viết đã được đăng thành công!"
                : "💾 Bài viết đã được lưu dưới dạng nháp.";

            return RedirectToAction(nameof(Index));
        }

        private (bool success, string message) ValidateImage(IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return (true, string.Empty);
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                return (false, "Chỉ cho phép tải lên ảnh PNG, JPG hoặc JPEG.");
            }
            const long maxSize = 2 * 1024 * 1024;
            if (image.Length > maxSize)
            {
                return (false, "Kích thước ảnh phải nhỏ hơn 2MB.");
            }
            return (true, string.Empty);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _newsService.GetByIdAsync(id);
            if (post == null) return NotFound();
            return View(post);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HealthNewsPost model, string action, IFormFile? uploadImage)
        {
            if (id != model.Id) return BadRequest();

            ModelState.Remove("AuthorName");
            ModelState.Remove("CreatedAt");

            if (!ModelState.IsValid)
                return View(model);

            var validation = ValidateImage(uploadImage);
            if (!validation.success)
            {
                ModelState.AddModelError("", validation.message);
                return View(model);
            }

            var updatedModel = await _newsService.UpdateAsync(id, model, action, _env.WebRootPath, uploadImage);
            if (updatedModel == null) return NotFound();

            TempData["Success"] = "✅ Bài viết đã được cập nhật!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var success = await _newsService.ApproveAsync(id);
            if (!success) return NotFound();
            TempData["Success"] = "✅ Bài viết đã được duyệt và đăng tải!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            var success = await _newsService.RejectAsync(id, rejectionReason);
            if (!success) return NotFound();
            TempData["Success"] = "⚠️ Đã từ chối bài viết. Tác giả sẽ phải chỉnh sửa lại.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var success = await _newsService.PublishAsync(id);
            if (!success) return NotFound();
            TempData["Success"] = "✅ Bài viết đã được đăng!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            var success = await _newsService.HideAsync(id);
            if (!success) return NotFound();
            TempData["Success"] = "🙈 Bài viết đã được ẩn.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _newsService.DeleteAsync(id);
            if (!success) return NotFound();
            TempData["Success"] = "🗑️ Bài viết đã bị xóa.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateFromPatientStats()
        {
            Console.WriteLine("[DEBUG] Bắt đầu GenerateFromPatientStats...");
            try
            {
                var vm  = await _statsService.GetDashboardStatisticsAsync();
                var ctx = BuildPatientStatsContext(vm);
                Console.WriteLine($"[DEBUG] Đã build xong context (chiều dài: {ctx.Length}). Đang gọi Gemini API...");

                var rawJson = await _gemini.GenerateHealthNewsAsync(ctx);
                Console.WriteLine($"[DEBUG] Đã nhận phản hồi từ Gemini (chiều dài: {rawJson.Length}).");

                var (title, summary, content) = ParseNewsJson(rawJson);
                Console.WriteLine($"[DEBUG] Parse JSON thành công. Tiêu đề: {title}");

                HttpContext.Session.SetString("AiNewsTitle",   title);
                HttpContext.Session.SetString("AiNewsSummary", summary);
                HttpContext.Session.SetString("AiNewsContent", content);
                HttpContext.Session.SetString("AiNewsSource",  "PatientStatistics");

                Console.WriteLine("[DEBUG] Đã lưu vào Session. Chuyển hướng sang trang Create...");
                return RedirectToAction(nameof(Create));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GenerateFromPatientStats lỗi: {ex}");
                TempData["Error"] = $"❌ AI gặp lỗi: {ex.Message}";
                return RedirectToAction("PatientStatistics", "AdminDashboard");
            }
        }

        [AllowAnonymous]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateNewsAjax([FromForm] string source, [FromForm] string userPrompt = "")
        {
            if (!User.Identity.IsAuthenticated || (!User.IsInRole("2") && !User.IsInRole("3")))
            {
                return Json(new { success = false, error = "Bạn không có quyền sử dụng tính năng này." });
            }

            Console.WriteLine($"[DEBUG] GenerateNewsAjax: source={source}, prompt={userPrompt}");
            try
            {
                string ctx;
                string resolvedSource;
                if (source == "HabitStatistics")
                {
                    var vm = await _statsService.GetHabitStatisticsAsync();
                    ctx = BuildHabitStatsContext(vm);
                    resolvedSource = "HabitStatistics";
                }
                else
                {
                    var vm = await _statsService.GetDashboardStatisticsAsync();
                    ctx = BuildPatientStatsContext(vm);
                    resolvedSource = "PatientStatistics";
                }

                var rawJson = await _gemini.GenerateHealthNewsAsync(ctx, userPrompt);
                var (title, summary, content) = ParseNewsJson(rawJson);

                return Json(new
                {
                    success = true,
                    title,
                    summary,
                    content,
                    source = resolvedSource
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GenerateNewsAjax: {ex}");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateFromHabitStats()
        {
            Console.WriteLine("[DEBUG] Bắt đầu GenerateFromHabitStats...");
            try
            {
                var vm  = await _statsService.GetHabitStatisticsAsync();
                var ctx = BuildHabitStatsContext(vm);
                Console.WriteLine($"[DEBUG] Đã build xong context (chiều dài: {ctx.Length}). Đang gọi Gemini API...");

                var rawJson = await _gemini.GenerateHealthNewsAsync(ctx);
                Console.WriteLine($"[DEBUG] Đã nhận phản hồi từ Gemini (chiều dài: {rawJson.Length}).");
                
                var (title, summary, content) = ParseNewsJson(rawJson);
                Console.WriteLine($"[DEBUG] Parse JSON thành công. Tiêu đề: {title}");

                HttpContext.Session.SetString("AiNewsTitle",   title);
                HttpContext.Session.SetString("AiNewsSummary", summary);
                HttpContext.Session.SetString("AiNewsContent", content);
                HttpContext.Session.SetString("AiNewsSource",  "HabitStatistics");

                Console.WriteLine("[DEBUG] Đã lưu vào Session. Chuyển hướng sang trang Create...");
                return RedirectToAction(nameof(Create));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GenerateFromHabitStats lỗi: {ex}");
                TempData["Error"] = $"❌ AI gặp lỗi: {ex.Message}";
                return RedirectToAction("HabitStatistics", "AdminDashboard");
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var post = await _newsService.GetByIdAsync(id);
            if (post == null || post.Status != "Published") return NotFound();
            return View(post);
        }

        private static string BuildPatientStatsContext(ViewModels.Admin.DashboardStatisticsViewModel vm)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== THỐNG KÊ NHÂN KHẨU HỌC & LÂM SÀNG ===");
            sb.AppendLine();
            sb.AppendLine("1. Phân bố độ tuổi bệnh nhân:");
            for (int i = 0; i < vm.Demographics.AgeLabels.Count; i++)
                sb.AppendLine($"   - {vm.Demographics.AgeLabels[i]}: {vm.Demographics.AgeValues[i]} người");

            sb.AppendLine();
            sb.AppendLine("2. Phân bố giới tính:");
            for (int i = 0; i < vm.Demographics.SexLabels.Count; i++)
                sb.AppendLine($"   - {vm.Demographics.SexLabels[i]}: {vm.Demographics.SexValues[i]} người");

            sb.AppendLine();
            sb.AppendLine("3. Phân bố loại đau ngực:");
            for (int i = 0; i < vm.Symptoms.ChestPainLabels.Count; i++)
                sb.AppendLine($"   - {vm.Symptoms.ChestPainLabels[i]}: {vm.Symptoms.ChestPainValues[i]} ca");

            sb.AppendLine();
            sb.AppendLine($"4. Cholesterol trung bình nhóm tuổi 40-50: {vm.Symptoms.AverageCholesterolAge40To50} mg/dl");
            sb.AppendLine($"5. Tỷ lệ đường huyết vượt 120mg/dl: {vm.Symptoms.FastingBsHighRate}%");

            return sb.ToString();
        }

        private static string BuildHabitStatsContext(ViewModels.Admin.HabitStatisticsViewModel vm)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== THỐNG KÊ THÓI QUEN HẰNG NGÀY CỦA BỆNH NHÂN ===");
            sb.AppendLine($"Tổng số bệnh nhân có dữ liệu thói quen: {vm.TotalPatientsWithHabit}/{vm.TotalPatients}");
            sb.AppendLine();

            sb.AppendLine("TOP 5 THÓI QUEN XẤU PHỔ BIẾN NHẤT:");
            for (int i = 0; i < vm.TopBadHabitLabels.Count; i++)
                sb.AppendLine($"   {i + 1}. {vm.TopBadHabitLabels[i]}: {vm.TopBadHabitValues[i]} người");

            sb.AppendLine();
            sb.AppendLine("TOP 5 THÓI QUEN TỐT PHỔ BIẾN NHẤT:");
            for (int i = 0; i < vm.TopGoodHabitLabels.Count; i++)
                sb.AppendLine($"   {i + 1}. {vm.TopGoodHabitLabels[i]}: {vm.TopGoodHabitValues[i]} người");

            sb.AppendLine();
            sb.AppendLine("PHÂN NHÓM THÓI QUEN:");
            foreach (var cat in vm.Categories)
            {
                sb.AppendLine($"\nNhóm [{cat.Name}]:");
                foreach (var item in cat.Items)
                    sb.AppendLine($"   - {item.Label}: {item.Count} người ({item.Percentage}%)");
            }

            return sb.ToString();
        }

        private static (string title, string summary, string content) ParseNewsJson(string raw)
        {
            raw = Regex.Replace(raw, @"```json\s*", "", RegexOptions.IgnoreCase);
            raw = Regex.Replace(raw, @"```\s*", "");
            raw = raw.Trim();

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root    = doc.RootElement;
                var title   = root.TryGetProperty("title",   out var t) ? t.GetString() ?? "" : "Tin tức sức khỏe";
                var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
                var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : raw;
                content = ConvertMarkdownToHtml(content);
                return (title, summary, content);
            }
            catch
            {
                return ("Tin tức sức khỏe mới", "", ConvertMarkdownToHtml(raw));
            }
        }

        private static string ConvertMarkdownToHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            bool hasHtml = Regex.IsMatch(text, @"<(h[1-6]|p|ul|ol|li|strong|em|br)\b", RegexOptions.IgnoreCase);

            var lines = text.Split('\n');
            var result = new System.Text.StringBuilder();
            bool inList = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                if (Regex.IsMatch(line, @"^[-*_]{3,}\s*$")) continue;

                var h3 = Regex.Match(line, @"^#{1,3}\s+(.+)");
                if (h3.Success)
                {
                    if (inList) { result.AppendLine("</ul>"); inList = false; }
                    var heading = ApplyInlineMarkdown(h3.Groups[1].Value);
                    result.AppendLine($"<h3>{heading}</h3>");
                    continue;
                }

                var bullet = Regex.Match(line, @"^[\*\-]\s+(.+)");
                if (bullet.Success)
                {
                    if (!inList) { result.AppendLine("<ul>"); inList = true; }
                    var item = ApplyInlineMarkdown(bullet.Groups[1].Value);
                    result.AppendLine($"<li>{item}</li>");
                    continue;
                }

                if (inList && !string.IsNullOrWhiteSpace(line))
                {
                    result.AppendLine("</ul>");
                    inList = false;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inList) { result.AppendLine("</ul>"); inList = false; }
                    continue;
                }

                if (hasHtml && Regex.IsMatch(line, @"^<"))
                {
                    result.AppendLine(line);
                }
                else
                {
                    var para = ApplyInlineMarkdown(line);
                    result.AppendLine($"<p>{para}</p>");
                }
            }

            if (inList) result.AppendLine("</ul>");

            return result.ToString().Trim();
        }

        private static string ApplyInlineMarkdown(string text)
        {
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            text = Regex.Replace(text, @"__(.+?)__",     "<strong>$1</strong>");
            text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
            text = Regex.Replace(text, @"_(.+?)_",   "<em>$1</em>");
            return text;
        }
    }
}
