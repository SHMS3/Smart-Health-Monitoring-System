using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "3")] // Assuming 3 is Receptionist
    public class ReceptionistNewsController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;

        public ReceptionistNewsController(
            SmartHealthMonitoringContext context,
            IWebHostEnvironment env,
            IEmailService emailService)
        {
            _context = context;
            _env = env;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var query = _context.HealthNewsPosts.AsQueryable();
            
            if (!string.IsNullOrEmpty(status))
                query = query.Where(n => n.Status == status);

            var news = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
            ViewBag.CurrentStatus = status ?? "all";
            
            // Get current receptionist name to determine edit rights in view
            ViewBag.CurrentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            
            return View(news);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new HealthNewsPost
            {
                Status = "Draft"
            };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HealthNewsPost model, string action, IFormFile? uploadImage)
        {
            ModelState.Remove("AuthorName");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("Status");
            ModelState.Remove("RejectionReason");

            if (!ModelState.IsValid)
                return View(model);

            // Handle image upload
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

            var authorName = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            model.AuthorName = authorName;
            model.CreatedAt  = DateTime.UtcNow;
            
            // Lễ tân có thể lưu nháp hoặc nộp duyệt
            model.Status = (action == "submit") ? "Pending" : "Draft";

            _context.HealthNewsPosts.Add(model);
            await _context.SaveChangesAsync();

            if (model.Status == "Pending")
            {
                TempData["Success"] = "Bài viết đã được gửi cho Quản trị viên duyệt.";
                // Send email to admin
                try
                {
                    string adminEmail = "namntp27@gmail.com";
                    string subject = "Yêu cầu duyệt bài viết tin tức mới";
                    string htmlContent = $"<p>Xin chào Admin,</p><p>Nhân viên lễ tân <b>{authorName}</b> vừa gửi yêu cầu duyệt một bài viết tin tức mới có tiêu đề: <b>{model.Title}</b>.</p><p>Vui lòng đăng nhập vào hệ thống để kiểm tra và duyệt bài.</p>";
                    _ = Task.Run(async () => 
                    {
                        await _emailService.SendEmailAsync(adminEmail, subject, htmlContent);
                    });
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error sending email: " + ex.Message);
                }
            }
            else
            {
                TempData["Success"] = "Bài viết đã được lưu dưới dạng nháp.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return NotFound();
            
            var currentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            
            // Lễ tân chỉ được sửa bài của mình và ở trạng thái Draft hoặc Pending
            if (post.AuthorName != currentAuthor || (post.Status != "Draft" && post.Status != "Pending"))
            {
                TempData["Error"] = "Bạn không có quyền chỉnh sửa bài viết này.";
                return RedirectToAction(nameof(Index));
            }
            
            return View(post);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HealthNewsPost model, string action, IFormFile? uploadImage)
        {
            if (id != model.Id) return BadRequest();

            ModelState.Remove("AuthorName");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("Status");
            ModelState.Remove("RejectionReason");

            if (!ModelState.IsValid)
                return View(model);

            var existing = await _context.HealthNewsPosts.FindAsync(id);
            if (existing == null) return NotFound();

            var currentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            if (existing.AuthorName != currentAuthor || (existing.Status != "Draft" && existing.Status != "Pending"))
            {
                TempData["Error"] = "Bạn không có quyền chỉnh sửa bài viết này.";
                return RedirectToAction(nameof(Index));
            }

            // Image upload
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

            existing.Title = model.Title;
            existing.Summary = model.Summary;
            existing.Content = model.Content;
            existing.Source = model.Source;
            existing.UpdatedAt = DateTime.UtcNow;

            existing.Status = (action == "submit") ? "Pending" : "Draft";
            
            if (existing.Status == "Pending")
            {
                existing.RejectionReason = null; // Clear rejection reason when resubmitted
            }

            await _context.SaveChangesAsync();

            if (existing.Status == "Pending")
            {
                TempData["Success"] = "Bài viết đã được cập nhật và gửi duyệt.";
                // Send email to admin
                try
                {
                    string adminEmail = "namntp27@gmail.com";
                    string subject = "Yêu cầu duyệt bài viết tin tức";
                    string htmlContent = $"<p>Xin chào Admin,</p><p>Nhân viên lễ tân <b>{currentAuthor}</b> vừa gửi/cập nhật yêu cầu duyệt bài viết tin tức: <b>{existing.Title}</b>.</p><p>Vui lòng đăng nhập vào hệ thống để kiểm tra và duyệt bài.</p>";
                    _ = Task.Run(async () => 
                    {
                        await _emailService.SendEmailAsync(adminEmail, subject, htmlContent);
                    });
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error sending email: " + ex.Message);
                }
            }
            else
            {
                TempData["Success"] = "Bài viết đã được lưu nháp.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bài viết." });
            }

            var currentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            if (post.AuthorName != currentAuthor || (post.Status != "Draft" && post.Status != "Pending"))
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bài viết này." });
            }

            _context.HealthNewsPosts.Remove(post);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xóa bài viết thành công." });
        }
    }
}
