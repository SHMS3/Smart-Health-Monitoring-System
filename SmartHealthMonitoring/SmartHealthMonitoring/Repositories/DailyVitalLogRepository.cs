using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Interfaces.Repositories;

namespace SmartHealthMonitoring.Repositories
{
    public class DailyVitalLogRepository
    : IDailyVitalLogRepository {
        private readonly SmartHealthMonitoringContext _context;

        public DailyVitalLogRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<DailyVitalLog>> GetAllDailyLogByPatientIdAsync(
            int patientId, DateTime? fromDate, DateTime? toDate, int pageIndex = 1, int pageSize = 10)
        {
            var query = _context.DailyVitalLogs
                .AsNoTracking()
                .Where(x => x.PatientId == patientId && !x.IsDeleted);

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.LoggedAt >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.LoggedAt <= endOfDay);
            }

            query = query.OrderByDescending(x => x.LoggedAt);

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<DailyVitalLog>
            {
                Items = items,
                TotalCount = totalCount,
                Page = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task CreateDailyLogAsync(DailyVitalLog entity)
        {
            _context.DailyVitalLogs.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<DailyVitalLog?> GetDailyLogByIdAsync(int id)
        {
            return await _context.DailyVitalLogs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task UpdateDailyLogAsync(DailyVitalLog entity)
        {
            _context.DailyVitalLogs.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task LockPreviousLogsAsync(int patientId)
        {
            var logs = await _context.DailyVitalLogs
                .Where(x => x.PatientId == patientId && !x.IsDeleted && !x.IsUpdateLocked)
                .ToListAsync();

            foreach (var log in logs)
            {
                log.IsUpdateLocked = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<PatientThreshold?> GetPatientThresholdAsync(int patientId)
        {
            return await _context.PatientThresholds
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PatientId == patientId);
        }
    }
}

