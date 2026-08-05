using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Interfaces.Clinical;

public interface IDailyVitalLogService
{
    Task<PagedResult<DailyVitalLogViewModel>> GetPatientVitalsHistoryAsync(int userId, DateTime? fromDate, DateTime? toDate, int pageIndex = 1, int pageSize = 10);
    Task CreateLogAsync(int userId, DailyVitalLogViewModel model);
    Task<DailyVitalLogViewModel?> GetDailyLogDetailsAsync(int id);
    Task<DailyVitalLogViewModel?> GetLogForUpdateAsync(int id);
    Task<bool> UpdateLogAsync(int id, DailyVitalLogViewModel model);
    Task<IEnumerable<DailyVitalLog>> GetLogsByDateAsync(int userId, DateTime date);
    Task<PersonalHealthTrackerViewModel> GetPatientHealthTrendsAsync(int userId, int days = 7);
}
