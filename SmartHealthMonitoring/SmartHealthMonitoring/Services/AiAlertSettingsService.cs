using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services;

public class AiAlertSettingsService : IAiAlertSettingsService
{
    private readonly SmartHealthMonitoringContext _context;

    public AiAlertSettingsService(SmartHealthMonitoringContext context)
    {
        _context = context;
    }

    public async Task<AiAlertSetting> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.AiAlertSettings
            .Include(s => s.UpdatedByAdmin)
            .FirstOrDefaultAsync(s => s.Id == AiAlertSetting.DefaultId, cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        settings = new AiAlertSetting
        {
            Id = AiAlertSetting.DefaultId,
            EmergencyRiskLevelThreshold = 3,
            EmergencyRiskScoreThreshold = 0.70m,
            EmergencyAgeMin = 0,
            EmergencyAgeMax = 120,
            EmergencySex = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AiAlertSettings.Add(settings);
        await _context.SaveChangesAsync(cancellationToken);

        return settings;
    }

    public async Task<AiAlertSetting> UpdateSettingsAsync(
        AdminAiAlertSettingsViewModel model,
        int? updatedByAdminId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);

        settings.EmergencyRiskLevelThreshold = model.EmergencyRiskLevelThreshold;
        settings.EmergencyRiskScoreThreshold = model.EmergencyRiskScoreThreshold;
        settings.EmergencyAgeMin = model.EmergencyAgeMin;
        settings.EmergencyAgeMax = model.EmergencyAgeMax;
        settings.EmergencySex = model.EmergencySex;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = updatedByAdminId;

        await _context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public bool IsHighPriority(AiriskPrediction prediction)
    {
        return prediction.RiskLevel >= 2;
    }

    public bool IsHighRisk(AiriskPrediction prediction, AiAlertSetting settings, Patient? patient = null)
    {
        return IsHighPriority(prediction);
    }
}
