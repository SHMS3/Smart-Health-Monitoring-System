 using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminNewsController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly GeminiService _gemini;
        private readonly IAdminStatisticsService _statsService;
        private readonly IWebHostEnvironment _env;

        public AdminNewsController(
            SmartHealthMonitoringContext context,
            GeminiService gemini,
            IAdminStatisticsService statsService,
            IWebHostEnvironment env)
        {
            _context = context;
            _gemini = gemini;
            _statsService = statsService;
            _env = env;
        }

        // ══════════════════════════════════════════════
        // INDEX — danh sách tất cả tin tức
        // ══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var query = _context.HealthNewsPosts.AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(n => n.Status == status);

            var news = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
            ViewBag.CurrentStatus = status ?? "all";
            return View(news);
        }

        // ══════════════════════════════════════════════
        // CREATE GET — trang tạo bài (form trắng hoặc prefill từ AI)
        // ══════════════════════════════════════════════
        [HttpGet]
        public IActionResult Create(string? source = null)
        {
            // Đọc dữ liệu AI từ Session (hỗ trợ nội dung lớn không giới hạn không gian cookie)
            var model = new HealthNewsPost
            {
                Title   = HttpContext.Session.GetString("AiNewsTitle")   ?? string.Empty,
                Summary = HttpContext.Session.GetString("AiNewsSummary") ?? string.Empty,
                Content = HttpContext.Session.GetString("AiNewsContent") ?? string.Empty,
                Source  = HttpContext.Session.GetString("AiNewsSource")  ?? source ?? "Manual",
                Status  = "Draft"
            };
            // Xóa session sau khi đã đọc xong
            HttpContext.Session.Remove("AiNewsTitle");
            HttpContext.Session.Remove("AiNewsSummary");
            HttpContext.Session.Remove("AiNewsContent");
            HttpContext.Session.Remove("AiNewsSource");
            return View(model);
        }

        // ══════════════════════════════════════════════
        // CREATE POST — lưu bài mới
        // ══════════════════════════════════════════════
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

            // Xử lý upload ảnh
            if (uploadImage != null && uploadImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "news");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(stream);
                }
                
                model.ThumbnailUrl = "/images/news/" + fileName;
            }

            var adminName = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
            model.AuthorName = adminName;
            model.CreatedAt  = SmartHealthMonitoring.Common.AppTime.Now;
            model.Status     = (action == "publish") ? "Published" : "Draft";
            if (model.Status == "Published")
                model.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;

            _context.HealthNewsPosts.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = model.Status == "Published"
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
            // 3. Validate dung lượng 2MB
            const long maxSize = 2 * 1024 * 1024;
            if (image.Length > maxSize)
            {
                return (false, "Kích thước ảnh phải nhỏ hơn 2MB.");
            }
            return (true, string.Empty);
        }

        // ══════════════════════════════════════════════
        // EDIT GET
        // ══════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            return View(post);
        }

        // ══════════════════════════════════════════════
        // EDIT POST
        // ══════════════════════════════════════════════
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

            var existing = await _context.HealthNewsPosts.FindAsync(id);
            if (existing == null) return NotFound();

            // Xử lý upload ảnh nếu có
            if (uploadImage != null && uploadImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "news");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(uploadImage.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadImage.CopyToAsync(stream);
                }
                
                existing.ThumbnailUrl = "/images/news/" + fileName;
            }
            else
            {
                existing.ThumbnailUrl = model.ThumbnailUrl; // Giữ nguyên hoặc url cũ
            }

            existing.Title        = model.Title;
            existing.Summary      = model.Summary;
            existing.Content      = model.Content;
            existing.UpdatedAt    = SmartHealthMonitoring.Common.AppTime.Now;

            if (action == "publish" && existing.Status != "Published")
            {
                existing.Status      = "Published";
                existing.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;
            }
            else if (action == "draft")
            {
                existing.Status = "Draft";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ Bài viết đã được cập nhật!";
            return RedirectToAction(nameof(Index));
        }

        // =======================================================================================
        // PUBLISH / HIDE / DELETE / APPROVE / REJECT (POST actions)
        // =======================================================================================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            post.Status = "Published";
            post.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;
            post.RejectionReason = null;
            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ Bài viết đã được duyệt và đăng tải!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectionReason)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            
            post.Status = "Draft";
            post.RejectionReason = rejectionReason;
            await _context.SaveChangesAsync();
            TempData["Success"] = "⚠️ Đã từ chối bài viết. Tác giả sẽ phải chỉnh sửa lại.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            post.Status      = "Published";
            post.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;
            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ Bài viết đã được đăng!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            post.Status = "Hidden";
            await _context.SaveChangesAsync();
            TempData["Success"] = "🙈 Bài viết đã được ẩn.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            _context.HealthNewsPosts.Remove(post);
            await _context.SaveChangesAsync();
            TempData["Success"] = "🗑️ Bài viết đã bị xóa.";
            return RedirectToAction(nameof(Index));
        }

        // ══════════════════════════════════════════════
        // AI GENERATE — từ PatientStatistics
        // ══════════════════════════════════════════════
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

                // Lưu vào Session (không giới hạn kích thước như cookie)
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

        // =======================================================================================
        // AI GENERATE AJAX (Từ trang Create)
        // =======================================================================================
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

        // ══════════════════════════════════════════════
        // AI GENERATE — từ HabitStatistics
        // ══════════════════════════════════════════════
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

                // Lưu vào Session (không giới hạn kích thước như cookie)
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

        // ══════════════════════════════════════════════
        // DETAIL — xem chi tiết (dùng cho trang bệnh nhân)
        // ══════════════════════════════════════════════
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null || post.Status != "Published") return NotFound();
            return View(post);
        }

        // ══════════════════════════════════════════════
        // PRIVATE HELPERS
        // ══════════════════════════════════════════════
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
            // Làm sạch markdown code fence nếu AI trả về
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
                // Chuyển Markdown → HTML phòng trường hợp AI vẫn trả Markdown
                content = ConvertMarkdownToHtml(content);
                return (title, summary, content);
            }
            catch
            {
                // Fallback: dùng toàn bộ raw làm content
                return ("Tin tức sức khỏe mới", "", ConvertMarkdownToHtml(raw));
            }
        }

        /// <summary>
        /// Chuyển đổi Markdown cơ bản sang HTML để đảm bảo hiển thị đúng trong TinyMCE
        /// dù AI có trả về Markdown thay vì HTML thuần.
        /// </summary>
        private static string ConvertMarkdownToHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Nếu đã có thẻ HTML thực sự thì chỉ clean nhẹ, không convert
            bool hasHtml = Regex.IsMatch(text, @"<(h[1-6]|p|ul|ol|li|strong|em|br)\b", RegexOptions.IgnoreCase);

            var lines = text.Split('\n');
            var result = new System.Text.StringBuilder();
            bool inList = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                // Bỏ dòng chỉ có dấu --- hoặc ***
                if (Regex.IsMatch(line, @"^[-*_]{3,}\s*$")) continue;

                // ### Heading 3
                var h3 = Regex.Match(line, @"^#{1,3}\s+(.+)");
                if (h3.Success)
                {
                    if (inList) { result.AppendLine("</ul>"); inList = false; }
                    var heading = ApplyInlineMarkdown(h3.Groups[1].Value);
                    result.AppendLine($"<h3>{heading}</h3>");
                    continue;
                }

                // Bullet list: - item hoặc * item
                var bullet = Regex.Match(line, @"^[\*\-]\s+(.+)");
                if (bullet.Success)
                {
                    if (!inList) { result.AppendLine("<ul>"); inList = true; }
                    var item = ApplyInlineMarkdown(bullet.Groups[1].Value);
                    result.AppendLine($"<li>{item}</li>");
                    continue;
                }

                // Đóng list nếu đang mở
                if (inList && !string.IsNullOrWhiteSpace(line))
                {
                    result.AppendLine("</ul>");
                    inList = false;
                }

                // Dòng trống
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (inList) { result.AppendLine("</ul>"); inList = false; }
                    continue;
                }

                // Nếu đã có HTML tag thì giữ nguyên, không bọc <p>
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

        /// <summary>Chuyển inline Markdown: **bold**, *italic* sang HTML</summary>
        private static string ApplyInlineMarkdown(string text)
        {
            // **bold** hoặc __bold__
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            text = Regex.Replace(text, @"__(.+?)__",     "<strong>$1</strong>");
            // *italic* hoặc _italic_
            text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
            text = Regex.Replace(text, @"_(.+?)_",   "<em>$1</em>");
            return text;
        }
    }
}
