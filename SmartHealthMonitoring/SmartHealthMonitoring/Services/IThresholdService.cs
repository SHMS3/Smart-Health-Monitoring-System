using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public interface IThresholdService
    {
       Task<ServiceResult> ValidateAndUpdateAsync(
            WarningAlert alert,
            int doctorId,
            short? systolicBpWarning,
            short? systolicBpDanger,
            short? diastolicBpWarning,
            short? diastolicBpDanger,
            short? heartRateWarningMin,
            short? heartRateDangerMin,
            short? heartRateWarningMax,
            short? heartRateDangerMax);
    }
}

