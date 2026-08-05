using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SmartHealthMonitoring.Interfaces.News;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services.News
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

            var imageValidation = ValidateImage(uploadImage);
            if (!imageValidation.success)
                return imageValidation;

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
            model.CreatedAt  = SmartHealthMonitoring.Common.AppTime.Now;
            
            model.Status = (action == "submit") ? "Pending" : "Draft";

            await _repository.AddNewsAsync(model);

            if (model.Status == "Pending")
            {
                try
                {
                    string adminEmail = "namntp27@gmail.com";
                    string subject = "Y�u c?u duy?t b�i vi?t tin t?c m?i";
                    string htmlContent = $"<p>Xin ch�o Admin,</p><p>Nh�n vi�n l? t�n <b>{authorName}</b> v?a g?i y�u c?u duy?t m?t b�i vi?t tin t?c m?i c� ti�u d?: <b>{model.Title}</b>.</p><p>Vui l�ng dang nh?p v�o h? th?ng d? ki?m tra v� duy?t b�i.</p>";
                    _ = Task.Run(async () => 
                    {
                        await _emailService.SendEmailAsync(adminEmail, subject, htmlContent);
                    });
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error sending email: " + ex.Message);
                }
                return (true, "B�i vi?t d� du?c g?i cho Qu?n tr? vi�n duy?t.");
            }
            else
            {
                return (true, "B�i vi?t d� du?c luu du?i d?ng nh�p.");
            }
        }

        public async Task<(bool success, string message, HealthNewsPost? post)> GetNewsForEditAsync(int id, string currentAuthor)
        {
            var post = await _repository.GetNewsByIdAsync(id);
            if (post == null) return (false, "Kh�ng t�m th?y b�i vi?t.", null);
            
            if (post.AuthorName != currentAuthor || (post.Status != "Draft" && post.Status != "Pending"))
            {
                return (false, "B?n kh�ng c� quy?n ch?nh s?a b�i vi?t n�y.", null);
            }
            
            return (true, "", post);
        }

        public async Task<(bool success, string message)> UpdateNewsAsync(int id, HealthNewsPost model, string action, IFormFile? uploadImage, string currentAuthor)
        {
            var existing = await _repository.GetNewsByIdAsync(id);
            if (existing == null) return (false, "Kh�ng t�m th?y b�i vi?t.");

            if (existing.AuthorName != currentAuthor || (existing.Status != "Draft" && existing.Status != "Pending"))
            {
                return (false, "B?n kh�ng c� quy?n ch?nh s?a b�i vi?t n�y.");
            }

            var imageValidation = ValidateImage(uploadImage);
            if (!imageValidation.success)
                return imageValidation;

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
            existing.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

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
                    string subject = "Y�u c?u duy?t b�i vi?t tin t?c";
                    string htmlContent = $"<p>Xin ch�o Admin,</p><p>Nh�n vi�n l? t�n <b>{currentAuthor}</b> v?a g?i/c?p nh?t y�u c?u duy?t b�i vi?t tin t?c: <b>{existing.Title}</b>.</p><p>Vui l�ng dang nh?p v�o h? th?ng d? ki?m tra v� duy?t b�i.</p>";
                    _ = Task.Run(async () => 
                    {
                        await _emailService.SendEmailAsync(adminEmail, subject, htmlContent);
                    });
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error sending email: " + ex.Message);
                }
                return (true, "B�i vi?t d� du?c c?p nh?t v� g?i duy?t.");
            }
            else
            {
                return (true, "B�i vi?t d� du?c luu nh�p.");
            }
        }

        public async Task<(bool success, string message)> DeleteNewsAsync(int id, string currentAuthor)
        {
            var post = await _repository.GetNewsByIdAsync(id);
            if (post == null)
            {
                return (false, "Kh�ng t�m th?y b�i vi?t.");
            }

            if (post.AuthorName != currentAuthor || (post.Status != "Draft" && post.Status != "Pending"))
            {
                return (false, "B?n kh�ng c� quy?n x�a b�i vi?t n�y.");
            }

            await _repository.DeleteNewsAsync(post);
            return (true, "X�a b�i vi?t th�nh c�ng.");
        }

        private (bool success, string message) ValidateImage(IFormFile? image)
        {
            if (image == null || image.Length == 0 || string.IsNullOrEmpty(image.FileName))
                return (true, string.Empty);
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                return (false, "Ch? cho ph�p ?nh PNG, JPG ho?c JPEG.");
            }
            const long maxSize = 2 * 1024 * 1024;
            if (image.Length > maxSize)
            {
                return (false, "K�ch thu?c ?nh ph?i nh? hon 2MB.");
            }
            return (true, string.Empty);
        }

    }
}

