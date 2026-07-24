using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces;
using System;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "0,1")] // Cho phép cả Bệnh nhân và Bác sĩ đi qua cổng Controller
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
                TempData["Error"] = "Lỗi khi tải hồ sơ y tế: " + ex.Message;

                if (User.IsInRole("1"))
                {
                    return RedirectToAction("DoctorQueue", "Appointment");
                }

                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [Authorize(Roles = "1")] // Chỉ Bác sĩ mới được quyền Hủy hồ sơ
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
                TempData["Error"] = "Lỗi hệ thống khi hủy hồ sơ.";
                return RedirectToAction("DoctorQueue", "Appointment");
            }
        }

        [HttpPost]
        [Authorize(Roles = "1")] // Chỉ Bác sĩ mới được cập nhật quyền xem
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
                TempData["Error"] = "Lỗi hệ thống khi cập nhật quyền xem.";
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
