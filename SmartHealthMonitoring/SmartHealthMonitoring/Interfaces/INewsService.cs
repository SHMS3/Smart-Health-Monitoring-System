using Microsoft.AspNetCore.Http;
using SmartHealthMonitoring.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces
{
    public interface INewsService
    {
        Task<List<HealthNewsPost>> GetNewsAsync(string? status);
        Task<(bool success, string message)> CreateNewsAsync(HealthNewsPost model, string action, IFormFile? uploadImage, string authorName);
        Task<(bool success, string message, HealthNewsPost? post)> GetNewsForEditAsync(int id, string currentAuthor);
        Task<(bool success, string message)> UpdateNewsAsync(int id, HealthNewsPost model, string action, IFormFile? uploadImage, string currentAuthor);
        Task<(bool success, string message)> DeleteNewsAsync(int id, string currentAuthor);
    }
}
