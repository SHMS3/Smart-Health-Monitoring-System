using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Services;

namespace SmartHealthMonitoring.Controllers
{
    public class WarningAlertController : Controller
    {
        private readonly IWarningAlertService _warningAlertService;

        public WarningAlertController(IWarningAlertService warningAlertService)
        {
            _warningAlertService = warningAlertService;
        }

        public async Task<IActionResult> Index(byte? status)
        {
            var alerts = await _warningAlertService
                .GetAlertsAsync(status);


            return View(alerts);
        }
    }
}
