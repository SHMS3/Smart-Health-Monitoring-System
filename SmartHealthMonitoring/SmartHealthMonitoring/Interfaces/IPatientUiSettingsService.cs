using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Interfaces;

public interface IPatientUiSettingsService
{
    Task<PatientUiSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<PatientUiSettings> UpdateSettingsAsync(
        PatientUiSettingsViewModel model,
        string? updatedByAdminName,
        CancellationToken cancellationToken = default);

    Task<PatientUiSettings> ResetToDefaultAsync(
        string? updatedByAdminName,
        CancellationToken cancellationToken = default);
}
