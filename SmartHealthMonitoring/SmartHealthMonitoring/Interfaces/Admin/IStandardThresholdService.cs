using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;
namespace SmartHealthMonitoring.Interfaces.Admin;
public interface IStandardThresholdService
{
    Task<List<StandardThreshold>> GetAllAsync();
    Task<StandardThreshold?> GetByIdAsync(int id);
    Task<StandardThreshold> CreateAsync(StandardThresholdViewModel model);
    Task<StandardThreshold?> UpdateAsync(int id, StandardThresholdViewModel model);
    Task<StandardThreshold?> ToggleActiveAsync(int id);
    Task<bool> DeleteAsync(int id);
}
