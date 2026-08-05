using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Repositories;

public interface INewsRepository
{
    Task<List<HealthNewsPost>> GetNewsAsync(string? status);
    Task<HealthNewsPost?> GetNewsByIdAsync(int id);
    Task AddNewsAsync(HealthNewsPost post);
    Task UpdateNewsAsync(HealthNewsPost post);
    Task DeleteNewsAsync(HealthNewsPost post);
}
