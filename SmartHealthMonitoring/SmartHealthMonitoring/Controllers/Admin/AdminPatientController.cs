using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminPatientController : Controller
    {
        private readonly IAdminPatientService _patientService;

        public AdminPatientController(IAdminPatientService patientService)
        {
            _patientService = patientService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var result = await _patientService.GetPatientsPagedAsync(page, pageSize);
            return View(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int userId, string? lockReason)
        {
            try
            {
                await _patientService.ToggleLockAsync(userId, lockReason);
                TempData["Success"] = "�� thay d?i tr?ng th�i kh�a t�i kho?n b?nh nh�n.";
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Kh�ng t�m th?y ngu?i d�ng.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}


