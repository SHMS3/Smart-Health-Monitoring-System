using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminDashboardController : Controller
    {
        private readonly IAdminDashboardService _dashboardService;
        private readonly IAdminStatisticsService _adminStatisticsService;

        public AdminDashboardController(
            IAdminDashboardService dashboardService, 
            IAdminStatisticsService adminStatisticsService)
        {
            _dashboardService = dashboardService;
            _adminStatisticsService = adminStatisticsService;
        }

        public async Task<IActionResult> Index()
        {
            var vm = await _dashboardService.GetDashboardSummaryAsync();
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> PatientStatistics()
        {
            var vm = await _adminStatisticsService.GetDashboardStatisticsAsync();
            return View("~/Views/AdminDashboard/Statistics/PatientStatistics.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> HabitStatistics()
        {
            var vm = await _adminStatisticsService.GetHabitStatisticsAsync();
            return View("~/Views/AdminDashboard/Statistics/HabitStatistics.cshtml", vm);
        }
    }
}
