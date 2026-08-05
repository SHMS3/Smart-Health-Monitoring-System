using SmartHealthMonitoring.Common;

using SmartHealthMonitoring.Context;

using SmartHealthMonitoring.Interfaces.AI;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Interfaces.Audit;

namespace SmartHealthMonitoring.Services.AI
{
    public class ThresholdService : IThresholdService
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IAuditLogService _auditLogService;

        public ThresholdService(
            SmartHealthMonitoringContext context,
            IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        public async Task<ServiceResult> ValidateAndUpdateAsync(
            WarningAlert alert,
            int doctorId,
            short? systolicBpWarning,
            short? systolicBpDanger,
            short? diastolicBpWarning,
            short? diastolicBpDanger,
            short? heartRateWarningMin,
            short? heartRateDangerMin,
            short? heartRateWarningMax,
            short? heartRateDangerMax)
        {
            if (!HasThresholdUpdate(
                systolicBpWarning,
                systolicBpDanger,
                diastolicBpWarning,
                diastolicBpDanger,
                heartRateWarningMin,
                heartRateDangerMin,
                heartRateWarningMax,
                heartRateDangerMax))
            {
                return ServiceResult.Ok();
            }

            var currentThreshold = alert.Patient.PatientThreshold;

            short sysWarn =
                systolicBpWarning ??
                currentThreshold?.SystolicBpWarning ??
                130;

            short sysDanger =
                systolicBpDanger ??
                currentThreshold?.SystolicBpDanger ??
                140;

            if (sysWarn >= sysDanger)
            {
                return ServiceResult.Fail(
                    "Ngu?ng c?nh b�o huy?t �p t�m thu ph?i nh? hon ngu?ng nguy hi?m.");
            }

            short diaWarn =
                diastolicBpWarning ??
                currentThreshold?.DiastolicBpWarning ??
                80;

            short diaDanger =
                diastolicBpDanger ??
                currentThreshold?.DiastolicBpDanger ??
                90;

            if (diaWarn >= diaDanger)
            {
                return ServiceResult.Fail(
                    "Ngu?ng c?nh b�o huy?t �p t�m truong ph?i nh? hon ngu?ng nguy hi?m.");
            }

            short hrWarnMin =
                heartRateWarningMin ??
                currentThreshold?.HeartRateWarningMin ??
                60;

            short hrDangerMin =
                heartRateDangerMin ??
                currentThreshold?.HeartRateDangerMin ??
                50;

            if (hrDangerMin >= hrWarnMin)
            {
                return ServiceResult.Fail(
                    "Ngu?ng nh?p tim nguy hi?m th?p ph?i nh? hon ngu?ng c?nh b�o.");
            }

            short hrWarnMax =
                heartRateWarningMax ??
                currentThreshold?.HeartRateWarningMax ??
                100;

            short hrDangerMax =
                heartRateDangerMax ??
                currentThreshold?.HeartRateDangerMax ??
                120;

            if (hrWarnMax >= hrDangerMax)
            {
                return ServiceResult.Fail(
                    "Ngu?ng nh?p tim c?nh b�o cao ph?i nh? hon ngu?ng nguy hi?m.");
            }

            var threshold = currentThreshold;

            bool isNew = threshold == null;

            if (isNew)
            {
                threshold = new PatientThreshold
                {
                    PatientId = alert.PatientId,

                    SystolicBpWarning = 130,
                    SystolicBpDanger = 140,

                    DiastolicBpWarning = 80,
                    DiastolicBpDanger = 90,

                    HeartRateWarningMin = 60,
                    HeartRateDangerMin = 50,

                    HeartRateWarningMax = 100,
                    HeartRateDangerMax = 120
                };

                _context.PatientThresholds.Add(threshold);
            }

            threshold.SystolicBpWarning =
                systolicBpWarning ??
                threshold.SystolicBpWarning;

            threshold.SystolicBpDanger =
                systolicBpDanger ??
                threshold.SystolicBpDanger;

            threshold.DiastolicBpWarning =
                diastolicBpWarning ??
                threshold.DiastolicBpWarning;

            threshold.DiastolicBpDanger =
                diastolicBpDanger ??
                threshold.DiastolicBpDanger;

            threshold.HeartRateWarningMin =
                heartRateWarningMin ??
                threshold.HeartRateWarningMin;

            threshold.HeartRateDangerMin =
                heartRateDangerMin ??
                threshold.HeartRateDangerMin;

            threshold.HeartRateWarningMax =
                heartRateWarningMax ??
                threshold.HeartRateWarningMax;

            threshold.HeartRateDangerMax =
                heartRateDangerMax ??
                threshold.HeartRateDangerMax;

            threshold.UpdatedAt = DateTime.Now;
            threshold.UpdatedByDoctorId = doctorId;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                isNew ? "Create" : "Update",
                "PatientThreshold",
                threshold.Id.ToString(),
                $"C?p nh?t ngu?ng c?nh b�o cho b?nh nh�n {alert.Patient.User.FullName}",
                alert.Patient.UserId,
                alert.Patient.User.FullName);

            return ServiceResult.Ok();
        }

        private static bool HasThresholdUpdate(
            short? systolicBpWarning,
            short? systolicBpDanger,
            short? diastolicBpWarning,
            short? diastolicBpDanger,
            short? heartRateWarningMin,
            short? heartRateDangerMin,
            short? heartRateWarningMax,
            short? heartRateDangerMax)
        {
            return
                systolicBpWarning.HasValue ||
                systolicBpDanger.HasValue ||
                diastolicBpWarning.HasValue ||
                diastolicBpDanger.HasValue ||
                heartRateWarningMin.HasValue ||
                heartRateDangerMin.HasValue ||
                heartRateWarningMax.HasValue ||
                heartRateDangerMax.HasValue;
        }
    }
}





