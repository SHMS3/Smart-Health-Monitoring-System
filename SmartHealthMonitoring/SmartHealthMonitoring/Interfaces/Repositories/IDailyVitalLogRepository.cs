using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.Interfaces.Repositories;

public interface IDailyVitalLogRepository
{
    Task<PagedResult<DailyVitalLog>> GetAllDailyLogByPatientIdAsync(int patientId, DateTime? fromDate, DateTime? toDate, int pageIndex = 1, int pageSize = 10);
    Task CreateDailyLogAsync(DailyVitalLog entity);
    Task<DailyVitalLog?> GetDailyLogByIdAsync(int id);
    Task UpdateDailyLogAsync(DailyVitalLog entity);
    Task LockPreviousLogsAsync(int patientId);
    Task<PatientThreshold?> GetPatientThresholdAsync(int patientId);
}
