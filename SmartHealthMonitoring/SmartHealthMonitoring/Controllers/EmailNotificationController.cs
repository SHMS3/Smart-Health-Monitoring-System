using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Interfaces.Doctor;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1,2")]
    public class EmailNotificationController : Controller
    {
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly IDoctorService _doctorService;

        public EmailNotificationController(IEmailNotificationService emailNotificationService, IDoctorService doctorService)
        {
            _emailNotificationService = emailNotificationService;
            _doctorService = doctorService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(byte? status, string? emailType, DateTime? fromDate, DateTime? toDate, string? keyword, int? patientId, string? sender, int page = 1)
        {
            int? doctorId = null;
            bool isDoctor = User.IsInRole("1");
            
            if (isDoctor)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdClaim, out var userId))
                    return Forbid();

                var doctor = await _doctorService.GetDoctorByUserIdAsync(userId);
                if (doctor == null)
                    return Forbid();
                    
                doctorId = doctor.Id;
            }

            var viewModel = await _emailNotificationService.GetFilteredAsync(
                doctorId, isDoctor, status, emailType, fromDate, toDate, keyword, patientId, sender, page, 10);

            return View(viewModel);
        }
    }
}
