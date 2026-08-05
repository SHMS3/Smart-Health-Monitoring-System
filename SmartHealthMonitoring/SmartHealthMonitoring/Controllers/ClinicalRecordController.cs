using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Clinical;
using System;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "0,1")] // Cho ph�p c? B?nh nh�n v� B�c si di qua c?ng Controller
    public class ClinicalRecordController : Controller
    {
        private readonly IClinicalRecordService _clinicalRecordService;

        public ClinicalRecordController(IClinicalRecordService clinicalRecordService)
        {
            _clinicalRecordService = clinicalRecordService;
        }

        [Authorize(Roles = "0")]
        public async Task<IActionResult> MyRecords()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Forbid();

            var patientId = await _clinicalRecordService.GetPatientIdByEmailAsync(email);

            if (patientId == null)
            {
                return Forbid();
            }

            return RedirectToAction(nameof(Index), new { id = patientId });
        }

        [HttpGet]
        [Authorize(Roles = "0,1")]
        public async Task<IActionResult> Index(int id, int page = 1, int pageSize = 10, int diaryPage = 1, int diaryPageSize = 10, DateTime? searchDate = null, string activeTab = "clinical-content")
        {
            try
            {
                var email = User.Identity?.Name ?? "";
                var isPatientRole = User.IsInRole("0");
                var isDoctorRole = User.IsInRole("1");

                var (success, message, viewModel, _) = await _clinicalRecordService.GetPatientRecordIndexViewModelAsync(
                    id, email, isPatientRole, isDoctorRole, page, pageSize, diaryPage, diaryPageSize, searchDate, activeTab);

                if (!success)
                {
                    if (message == "Forbidden") return Forbid();

                    TempData["Error"] = message;
                    return isDoctorRole ? RedirectToAction("DoctorQueue", "Appointment") : RedirectToAction("Index", "Home");
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "L?i khi t?i h? so y t?: " + ex.Message;

                if (User.IsInRole("1"))
                {
                    return RedirectToAction("DoctorQueue", "Appointment");
                }

                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [Authorize(Roles = "1")] // Ch? B�c si m?i du?c quy?n H?y h? so
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var (success, message, redirectPatientId) = await _clinicalRecordService.DeleteClinicalRecordAsync(id);

                if (success)
                {
                    TempData["Success"] = message;
                    return RedirectToAction(nameof(Index), new { id = redirectPatientId });
                }
                else
                {
                    TempData["Error"] = message;
                    if (redirectPatientId.HasValue)
                    {
                        return RedirectToAction(nameof(Index), new { id = redirectPatientId });
                    }
                    return RedirectToAction("DoctorQueue", "Appointment");
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "L?i h? th?ng khi h?y h? so.";
                return RedirectToAction("DoctorQueue", "Appointment");
            }
        }

        [HttpPost]
        [Authorize(Roles = "1")] // Ch? B�c si m?i du?c c?p nh?t quy?n xem
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleViewForPatient(int id)
        {
            try
            {
                var (success, message, redirectPatientId) = await _clinicalRecordService.ToggleViewForPatientAsync(id);

                if (success)
                {
                    TempData["Success"] = message;
                    return RedirectToAction(nameof(Index), new { id = redirectPatientId });
                }
                else
                {
                    TempData["Error"] = message;
                    return RedirectToAction("DoctorQueue", "Appointment");
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "L?i h? th?ng khi c?p nh?t quy?n xem.";
                return RedirectToAction("DoctorQueue", "Appointment");
            }
        }

        [HttpGet]
        [Authorize(Roles = "0,1")]
        public async Task<IActionResult> Index_ListPatient(int id, int page = 1, int pageSize = 10, int diaryPage = 1, int diaryPageSize = 10, DateTime? searchDate = null, string activeTab = "clinical-content")
        {
            return await Index(id, page, pageSize, diaryPage, diaryPageSize, searchDate, activeTab);
        }
    }
}
