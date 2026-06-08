using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI
{
    /// <summary>
    /// Scoped Service triển khai logic quản lý WarningAlert:
    /// phân trang + tìm kiếm theo keyword, claim, resolve.
    /// </summary>
    public class AiWarningAlertService : IAiWarningAlertService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AiWarningAlertService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<bool> ClaimAlertAsync(int alertId, int doctorId)
        {
            var alert = await _context.WarningAlerts
                .FirstOrDefaultAsync(x => x.Id == alertId && !x.IsDeleted);

            if (alert == null)
            {
                return false;
            }

            // đã có người claim
            if (alert.Status != 0)
            {
                return false;
            }

            // update
            alert.ClaimedByDoctorId = doctorId;

            alert.ClaimedAt = DateTime.Now;

            alert.Status = 1;

            try
            {
                await _context.SaveChangesAsync();
                return true;

            }
            catch (DbUpdateConcurrencyException)
            {
                // người khác claim trước
                return false;
            }
        }

        public async Task<List<WarningAlert>> GetAlertsAsync(byte? status, string? keyword, int page, int pageSize)
        {
            var query = _context.WarningAlerts
                .Where(x => !x.IsDeleted)
                .Include(x => x.Patient)
                    .ThenInclude(p => p.User)
                .Include(x => x.ClaimedByDoctor)
                    .ThenInclude(d => d.User)
                .Include(x => x.Prediction)
                .AsQueryable();

            // Filter Status
            if (status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == status.Value);
            }

            // Search Patient Name
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.Patient.User.FullName
                        .Contains(keyword));
            }

            return await query
                .OrderByDescending(x => x.FlaggedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalAlertsAsync(byte? status, string? keyword)
        {
            var query = _context.WarningAlerts
                .Where(x => !x.IsDeleted)
                .Include(x => x.Patient)
                    .ThenInclude(p => p.User)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x =>
                    x.Patient.User.FullName
                        .Contains(keyword));
            }

            return await query.CountAsync();
        }

        public async Task<bool> ResolveAlertAsync(int alertId, int doctorId, string resolutionNote)
        {
            var alert = await _context.WarningAlerts
                .FirstOrDefaultAsync(x =>
                    x.Id == alertId &&
                    !x.IsDeleted);

            if (alert == null)
            {
                return false;
            }

            // phải đang processing
            if (alert.Status != 1)
            {
                return false;
            }

            // chỉ doctor đã claim mới resolve được
            if (alert.ClaimedByDoctorId != doctorId)
            {
                return false;
            }

            alert.Status = 2;

            alert.ResolutionNote = resolutionNote;

            try
            {
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
