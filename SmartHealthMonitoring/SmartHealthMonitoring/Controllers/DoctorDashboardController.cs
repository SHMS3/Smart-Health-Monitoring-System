using SmartHealthMonitoring.Interfaces.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Interfaces.Doctor;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using SmartHealthMonitoring.ViewModels.Doctor;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")]
    public class DoctorDashboardController : Controller
    {
        private readonly IDoctorDashboardService _dashboardService;
        private readonly IEmailTriggerService _emailTriggerService;

        public DoctorDashboardController(
            IDoctorDashboardService dashboardService,
            IEmailTriggerService emailTriggerService)
        {
            _dashboardService = dashboardService;
            _emailTriggerService = emailTriggerService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int userId))
                {
                    var currentDoctor = await _dashboardService.GetDoctorByUserIdAsync(userId);
                    ViewBag.IsOnShift = currentDoctor?.IsOnShift ?? false;
                    ViewBag.UnresolvedAlertCount = await _dashboardService.GetUnresolvedAlertCountAsync();
                }

                var result = await _dashboardService.GetPatientListAsync(page, pageSize);
                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tải dữ liệu: " + ex.Message;
                return View(new PagedResult<PatientListViewModel>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleShift()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "Không xác định được tài khoản." });

            var result = await _dashboardService.ToggleShiftAsync(userId);
            if (result == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });
            }

            string status = result ? "Đang trực" : "Ngoài ca";
            return Json(new { success = true, isOnShift = result, message = $"Đã chuyển trạng thái: {status}" });
        }

        [HttpGet("DoctorDashboard/PatientProfile/{patientId}")]
        public async Task<IActionResult> PatientProfile(int patientId, int page = 1)
        {
            int pageSize = 5; 
            var model = await _dashboardService.GetPatientProfileAsync(patientId, page, pageSize);
            
            if (model == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin bệnh nhân.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> WaitingList(int page = 1)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var doctor = await _dashboardService.GetDoctorByUserIdAsync(userId);
                if (doctor != null)
                {
                    var (model, patientsWithPayments) = await _dashboardService.GetWaitingListAsync(doctor.Id, page, 10);
                    ViewBag.PatientsWithPayments = patientsWithPayments;
                    return View(model);
                }
            }

            TempData["Error"] = "Không tìm thấy hồ sơ bác sĩ.";
            return View(new SmartHealthMonitoring.Common.PagedResult<WaitingPatient>());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelExam([FromBody] CancelExamRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "Không xác định được tài khoản bác sĩ." });

            var doctor = await _dashboardService.GetDoctorByUserIdAsync(userId);
            if (doctor == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

            var success = await _dashboardService.CancelExamAsync(request.WaitingId, doctor.Id);
            if (success)
            {
                return Json(new { success = true, message = "Đã hủy khám thành công." });
            }
            return Json(new { success = false, message = "Không thể hủy ca khám này." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteExam(int patientId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return RedirectToAction("DoctorQueue", "Appointment");

            var doctor = await _dashboardService.GetDoctorByUserIdAsync(userId);
            if (doctor == null) return RedirectToAction("DoctorQueue", "Appointment");

            var success = await _dashboardService.CompleteExamAsync(patientId, doctor.Id);
            if (success)
            {
                TempData["Success"] = "Đã hoàn tất khám cho bệnh nhân.";
            }

            return RedirectToAction("DoctorQueue", "Appointment");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptPatient([FromBody] AcceptPatientRequest request)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdString, out int userId))
                    return Json(new { success = false, message = "Không xác định được tài khoản bác sĩ." });

                var doctor = await _dashboardService.GetDoctorByUserIdAsync(userId);
                if (doctor == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

                var (success, patientId, message) = await _dashboardService.AcceptPatientAsync(request.WaitingId, doctor.Id);
                
                if (!success)
                {
                    return Json(new { success = false, message = message });
                }

                try
                {
                    await _emailTriggerService.SendDoctorAcceptedCheckInAsync(request.WaitingId, doctor.Id);
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"[AcceptPatient Email] {emailEx.Message}");
                }

                return Json(new { success = true, patientId = patientId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tiếp nhận: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServices()
        {
            var services = await _dashboardService.GetActiveServicesAsync();
            return Json(new { success = true, data = services });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(userIdString, out int userId))
                    return Json(new { success = false, message = "Không xác định được tài khoản bác sĩ." });

                var doctor = await _dashboardService.GetDoctorByUserIdAsync(userId);
                if (doctor == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

                var (success, message) = await _dashboardService.CreatePaymentAsync(request, doctor.Id);

                return Json(new { success = success, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tạo yêu cầu thanh toán: " + ex.Message });
            }
        }
    }
}


