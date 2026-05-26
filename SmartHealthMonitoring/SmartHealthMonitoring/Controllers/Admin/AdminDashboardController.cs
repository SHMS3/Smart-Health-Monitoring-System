using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminDashboardController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        public AdminDashboardController(SmartHealthMonitoringContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var vm = new AdminDashboardViewModel
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
                                          WarningLevel = "Mức độ " + pr.RiskLevel,
                                          FlaggedAt = wa.FlaggedAt
                                      }).Take(5).ToListAsync()
            };
            return View(vm);
        }
    }
}
