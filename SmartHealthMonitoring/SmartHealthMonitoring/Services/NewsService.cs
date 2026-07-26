using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services
{
    public class NewsService : INewsService
    {
        private readonly NewsRepository _repository;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;

        public NewsService(NewsRepository repository, IWebHostEnvironment env, IEmailService emailService)
        {
            _repository = repository;
            _env = env;
            _emailService = emailService;
        }

        public async Task<List<HealthNewsPost>> GetNewsAsync(string? status)
        {
            return await _repository.GetNewsAsync(status);
        }

        public async Task<(bool success, string message)> CreateNewsAsync(HealthNewsPost model, string action, IFormFile? uploadImage, string authorName)
        {
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

            model.AuthorName = authorName;
            model.CreatedAt  = DateTime.UtcNow;
            
            model.Status = (action == "submit") ? "Pending" : "Draft";

            await _repository.AddNewsAsync(model);

            if (model.Status == "Pending")
            {
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
                return (true, "Bài viết đã được gửi cho Quản trị viên duyệt.");
            }
            else
            {
                return (true, "Bài viết đã được lưu dưới dạng nháp.");
            }
        }

        public async Task<(bool success, string message, HealthNewsPost? post)> GetNewsForEditAsync(int id, string currentAuthor)
        {
            var post = await _repository.GetNewsByIdAsync(id);
            if (post == null) return (false, "Không tìm thấy bài viết.", null);
            
            if (post.AuthorName != currentAuthor || (post.Status != "Draft" && post.Status != "Pending"))
            {
                return (false, "Bạn không có quyền chỉnh sửa bài viết này.", null);
            }
            
            return (true, "", post);
        }

        public async Task<(bool success, string message)> UpdateNewsAsync(int id, HealthNewsPost model, string action, IFormFile? uploadImage, string currentAuthor)
        {
            var existing = await _repository.GetNewsByIdAsync(id);
            if (existing == null) return (false, "Không tìm thấy bài viết.");

            if (existing.AuthorName != currentAuthor || (existing.Status != "Draft" && existing.Status != "Pending"))
            {
                return (false, "Bạn không có quyền chỉnh sửa bài viết này.");
            }

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
                existing.RejectionReason = null; 
            }

            await _repository.UpdateNewsAsync(existing);

            if (existing.Status == "Pending")
            {
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
                return (true, "Bài viết đã được cập nhật và gửi duyệt.");
            }
            else
            {
                return (true, "Bài viết đã được lưu nháp.");
            }
        }

        public async Task<(bool success, string message)> DeleteNewsAsync(int id, string currentAuthor)
        {
            var post = await _repository.GetNewsByIdAsync(id);
            if (post == null)
            {
                return (false, "Không tìm thấy bài viết.");
            }

            if (post.AuthorName != currentAuthor || (post.Status != "Draft" && post.Status != "Pending"))
            {
                return (false, "Bạn không có quyền xóa bài viết này.");
            }

            await _repository.DeleteNewsAsync(post);
            return (true, "Xóa bài viết thành công.");
        }
    }
}
