using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Interfaces;

public interface IAiAlertSettingsService
{
    Task<AiAlertSetting> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AiAlertSetting> UpdateSettingsAsync(
        AdminAiAlertSettingsViewModel model,
        int? updatedByAdminId,
        CancellationToken cancellationToken = default);

    bool IsHighPriority(AiriskPrediction prediction);

    bool IsHighRisk(AiriskPrediction prediction, AiAlertSetting settings, Patient? patient = null);
}
