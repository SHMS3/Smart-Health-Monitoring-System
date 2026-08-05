using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.Admin
{
    public class AdminNewsService : IAdminNewsService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AdminNewsService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<List<HealthNewsPost>> GetAllAsync(string? status)
        {
            var query = _context.HealthNewsPosts.AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(n => n.Status == status);

            return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
        }

        public async Task<HealthNewsPost?> GetByIdAsync(int id)
        {
            return await _context.HealthNewsPosts.FindAsync(id);
        }

        public async Task<HealthNewsPost> CreateAsync(HealthNewsPost model, string authorName, string action, string? webRootPath, IFormFile? uploadImage)
        {
            if (uploadImage != null && uploadImage.Length > 0 && !string.IsNullOrEmpty(webRootPath))
            {
                var uploadsFolder = Path.Combine(webRootPath, "images", "news");
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
            model.CreatedAt = SmartHealthMonitoring.Common.AppTime.Now;
            model.Status = (action == "publish") ? "Published" : "Draft";
            if (model.Status == "Published")
                model.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;

            _context.HealthNewsPosts.Add(model);
            await _context.SaveChangesAsync();
            return model;
        }

        public async Task<HealthNewsPost?> UpdateAsync(int id, HealthNewsPost model, string action, string? webRootPath, IFormFile? uploadImage)
        {
            var existing = await _context.HealthNewsPosts.FindAsync(id);
            if (existing == null) return null;

            if (uploadImage != null && uploadImage.Length > 0 && !string.IsNullOrEmpty(webRootPath))
            {
                var uploadsFolder = Path.Combine(webRootPath, "images", "news");
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
                existing.ThumbnailUrl = model.ThumbnailUrl;
            }

            existing.Title = model.Title;
            existing.Summary = model.Summary;
            existing.Content = model.Content;
            existing.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

            if (action == "publish" && existing.Status != "Published")
            {
                existing.Status = "Published";
                existing.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;
            }
            else if (action == "draft")
            {
                existing.Status = "Draft";
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> ApproveAsync(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return false;
            post.Status = "Published";
            post.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;
            post.RejectionReason = null;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectAsync(int id, string rejectionReason)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return false;
            post.Status = "Draft";
            post.RejectionReason = rejectionReason;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PublishAsync(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return false;
            post.Status = "Published";
            post.PublishedAt = SmartHealthMonitoring.Common.AppTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HideAsync(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return false;
            post.Status = "Hidden";
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var post = await _context.HealthNewsPosts.FindAsync(id);
            if (post == null) return false;
            _context.HealthNewsPosts.Remove(post);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
