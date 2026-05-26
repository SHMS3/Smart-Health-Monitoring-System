using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public class WarningAlertService : IWarningAlertService
    {
        private readonly SmartHealthMonitoringContext _context;

        public WarningAlertService (SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        
        public async Task<List<WarningAlert>> GetAlertsAsync(byte? status)
        {
            var query = _context.WarningAlerts
                .Where(x => !x.IsDeleted)
                
                .Include(x => x.Patient)
                .ThenInclude(p => p.User)
                .Include(x => x.ClaimedByDoctor)
                .ThenInclude(d => d.User)
                .Include(x => x.Prediction)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            return await query
                .OrderByDescending(x => x.FlaggedAt)
                .ToListAsync();
        }
    }
}
