using Microsoft.AspNetCore.Http;
using SmartHealthMonitoring.Models;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IAdminNewsService
{
    Task<List<HealthNewsPost>> GetAllAsync(string? status);
    Task<HealthNewsPost?> GetByIdAsync(int id);
    Task<HealthNewsPost> CreateAsync(HealthNewsPost model, string authorName, string action, string? webRootPath, IFormFile? uploadImage);
    Task<HealthNewsPost?> UpdateAsync(int id, HealthNewsPost model, string action, string? webRootPath, IFormFile? uploadImage);
    Task<bool> ApproveAsync(int id);
    Task<bool> RejectAsync(int id, string rejectionReason);
    Task<bool> PublishAsync(int id);
    Task<bool> HideAsync(int id);
    Task<bool> DeleteAsync(int id);
}
