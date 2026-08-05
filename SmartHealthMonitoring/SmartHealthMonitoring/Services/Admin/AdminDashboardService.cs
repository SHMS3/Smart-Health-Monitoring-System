using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Services.Admin
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly SmartHealthMonitoringContext _context;

        public AdminDashboardService(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<AdminDashboardViewModel> GetDashboardSummaryAsync()
        {
            return new AdminDashboardViewModel
            {
                TotalDoctors = await _context.Users.CountAsync(u => u.Role == 1 && !u.IsDeleted),
                TotalPatients = await _context.Users.CountAsync(u => u.Role == 0 && !u.IsDeleted),
                TotalClinicalRecords = await _context.ClinicalRecords.CountAsync(c => !c.IsDeleted),
                TotalPendingAlerts = await _context.WarningAlerts.CountAsync(a => a.Status == 0 && !a.IsDeleted),

                RecentAlerts = await (from wa in _context.WarningAlerts
                                      join p in _context.Patients on wa.PatientId equals p.Id
                                      join u in _context.Users on p.UserId equals u.Id
                                      join pr in _context.AiriskPredictions on wa.PredictionId equals pr.Id
                                      where wa.Status == 0 && !wa.IsDeleted
                                      orderby wa.FlaggedAt descending
                                      select new RecentAlertViewModel
                                      {
                                          AlertId = wa.Id,
                                          PatientName = u.FullName,
                                          WarningLevel = "M?c d? " + pr.RiskLevel,
                                          FlaggedAt = wa.FlaggedAt
                                      }).Take(5).ToListAsync()
            };
        }
    }
}
