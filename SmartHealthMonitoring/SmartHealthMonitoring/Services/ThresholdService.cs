using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
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
                    "Ngưỡng cảnh báo huyết áp tâm thu phải nhỏ hơn ngưỡng nguy hiểm.");
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
                    "Ngưỡng cảnh báo huyết áp tâm trương phải nhỏ hơn ngưỡng nguy hiểm.");
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
                    "Ngưỡng nhịp tim nguy hiểm thấp phải nhỏ hơn ngưỡng cảnh báo.");
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
                    "Ngưỡng nhịp tim cảnh báo cao phải nhỏ hơn ngưỡng nguy hiểm.");
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
                $"Cập nhật ngưỡng cảnh báo cho bệnh nhân {alert.Patient.User.FullName}",
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