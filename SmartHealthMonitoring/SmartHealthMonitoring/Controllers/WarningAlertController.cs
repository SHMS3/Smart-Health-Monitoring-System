using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Services;
using System.Security.Claims;


namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "Doctor")]

    public class WarningAlertController : Controller
    {
        private readonly IWarningAlertService _warningAlertService;

        private readonly IDoctorService _doctorService;

        public WarningAlertController(IWarningAlertService warningAlertService, IDoctorService doctorService)
        {

            _warningAlertService = warningAlertService;
            _doctorService = doctorService;
        }

        public async Task<IActionResult> Dashboard(byte? status)
        {
            var alerts = await _warningAlertService
                .GetAlertsAsync(status);


            return View(alerts);
        }

        //Claim WarningAlert by Doctor

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim(int id)
        {
            // lấy user login
            var userIdString = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized();
            }

            int userId = int.Parse(userIdString);

            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);

            if (doctor == null)
            {
                TempData["Error"] =
                 "Doctor not found";

                return RedirectToAction(
                    "Dashboard");
            }

            var success = await _warningAlertService
                .ClaimAlertAsync(id, doctor.Id);

            if (success)
            {
                TempData["Success"] =
                "Claim alert successfully";
            }
            else
            {
                TempData["Error"] =
               "Alert already claimed";
            }
            return RedirectToAction(
            "Dashboard");

        }
    }
}
