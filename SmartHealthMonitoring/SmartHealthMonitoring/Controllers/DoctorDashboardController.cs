using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")]
    public class DoctorDashboardController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IEmailTriggerService _emailTriggerService;

        public DoctorDashboardController(
            SmartHealthMonitoringContext context,
            IEmailTriggerService emailTriggerService)
        {
            _context = context;
            _emailTriggerService = emailTriggerService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Doctor? currentDoctor = null;

                if (int.TryParse(userIdString, out int userId))
                {
                    currentDoctor = await _context.Doctors
                        .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);

                    ViewBag.IsOnShift = currentDoctor?.IsOnShift ?? false;

                    ViewBag.UnresolvedAlertCount = await _context.WarningAlerts
                        .CountAsync(w => w.Status == 0 && !w.IsDeleted);
                }

                // ─── Danh sách bệnh nhân (phân trang) ────────────────────────
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var query = _context.Patients
                    .Include(p => p.User)
                    .Where(p => !p.IsDeleted && !p.User.IsDeleted && p.User.Role == 0);

                int totalRecords = await query.CountAsync();

                var items = await query
                    .OrderByDescending(p => p.User.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PatientListViewModel
                    {
                        PatientId = p.Id,
                        FullName = p.User.FullName,
                        Age = today.Year - p.DateOfBirth.Year - (today.DayOfYear < p.DateOfBirth.DayOfYear ? 1 : 0),
                        SexDisplay = p.Sex == 1 ? "Nam" : "Nữ",
                        Phone = p.Phone ?? "N/A"
                    })
                    .ToListAsync();

                var result = new PagedResult<PatientListViewModel>
                {
                    Items = items,
                    TotalCount = totalRecords,
                    Page = page,
                    PageSize = pageSize
                };

                return View(result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi tải dữ liệu: " + ex.Message;
                return View(new PagedResult<PatientListViewModel>());
            }
        }


        /// <summary>
        /// AJAX: Bác sĩ tự gạt công tắc ca trực (bật/tắt thủ công)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleShift()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "Không xác định được tài khoản." });

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (doctor == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

            doctor.IsOnShift = !doctor.IsOnShift;
            await _context.SaveChangesAsync();

            string status = doctor.IsOnShift ? "Đang trực" : "Ngoài ca";
            return Json(new { success = true, isOnShift = doctor.IsOnShift, message = $"Đã chuyển trạng thái: {status}" });
        }


        [HttpGet("DoctorDashboard/PatientProfile/{patientId}")]
        public async Task<IActionResult> PatientProfile(int patientId, int page = 1)
        {
            int pageSize = 5; // Hiển thị 5 kết quả AI mỗi trang cho gọn màn hình

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);

            if (patient == null)
            {
                TempData["Error"] = "Không tìm thấy thông tin bệnh nhân.";
                return RedirectToAction(nameof(Index));
            }

            // 1. Tách Query của AI ra để phân trang
            var aiQuery = _context.AiriskPredictions
                .Where(a => a.PatientId == patientId && !a.IsDeleted);

            int totalAiCount = await aiQuery.CountAsync();

            var pagedAiItems = await aiQuery
                .OrderByDescending(a => a.PredictedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 2. Gắn vào Model
            var model = new PatientProfileViewModel
            {
                Patient = patient,
                ClinicalRecords = await _context.ClinicalRecords
                    .Where(c => c.PatientId == patientId && !c.IsDeleted)
                    .OrderByDescending(c => c.VisitDate)
                    .ToListAsync(),
                DailyVitalLogs = await _context.DailyVitalLogs
                    .Where(d => d.PatientId == patientId && !d.IsDeleted)
                    .OrderByDescending(d => d.LoggedAt)
                    .Take(30)
                    .ToListAsync(),

                // ĐÓNG GÓI VÀO PAGED RESULT
                AiPredictions = new PagedResult<AiriskPrediction>
                {
                    Items = pagedAiItems,
                    TotalCount = totalAiCount,
                    Page = page,
                    PageSize = pageSize
                },

                WarningAlerts = await _context.WarningAlerts
                    .Where(w => w.PatientId == patientId && !w.IsDeleted)
                    .OrderByDescending(w => w.FlaggedAt)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> WaitingList(int page = 1)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdString, out int userId);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);

            if (doctor == null)
            {
                TempData["Error"] = "Không tìm thấy hồ sơ bác sĩ.";
                return View(new SmartHealthMonitoring.Common.PagedResult<WaitingPatient>());
            }

            int pageSize = 10;
            var today = DateTime.UtcNow.Date;

            // Chỉ lấy bệnh nhân được gán cho bác sĩ này (DoctorId == doctor.Id)
            var query = _context.WaitingPatients
                .Include(w => w.Patient).ThenInclude(p => p.User)
                .Where(w => w.CreatedAt >= today
                         && w.DoctorId == doctor.Id
                         && (w.Status == 0 || w.Status == 1));

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var waitingPatients = await query
                .OrderBy(w => w.SequenceNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new SmartHealthMonitoring.Common.PagedResult<WaitingPatient>
            {
                Items = waitingPatients,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            var patientIds = waitingPatients.Select(w => w.PatientId).ToList();
            ViewBag.PatientsWithPayments = await _context.Payments
                .Where(p => patientIds.Contains(p.PatientId) && p.CreatedAt.Date == today)
                .Select(p => p.PatientId)
                .Distinct()
                .ToListAsync();

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelExam([FromBody] CancelExamRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdString, out int userId))
                return Json(new { success = false, message = "Không xác định được tài khoản bác sĩ." });

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (doctor == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

            var waiting = await _context.WaitingPatients.FirstOrDefaultAsync(w => w.Id == request.WaitingId && w.Status == 1 && w.DoctorId == doctor.Id);
            if (waiting != null)
            {
                waiting.Status = 2; // Đã hủy (Cancelled)
                await _context.SaveChangesAsync();
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

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
            if (doctor == null) return RedirectToAction("DoctorQueue", "Appointment");

            var activeWaiting = await _context.WaitingPatients
                .FirstOrDefaultAsync(w => w.PatientId == patientId && (w.Status == 0 || w.Status == 1) && w.DoctorId == doctor.Id);
            
            if (activeWaiting != null)
            {
                activeWaiting.Status = 3;
                await _context.SaveChangesAsync();
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

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

                var waitingPatient = await _context.WaitingPatients.AsNoTracking().FirstOrDefaultAsync(w => w.Id == request.WaitingId);
                if (waitingPatient == null)
                    return Json(new { success = false, message = "Không tìm thấy bệnh nhân trong hàng đợi." });

                if (waitingPatient.Status != 0)
                    return Json(new { success = false, message = "Bệnh nhân này đã được tiếp nhận hoặc đã hủy." });

                // Dùng ExecuteUpdateAsync để cập nhật trực tiếp xuống DB (Atomic Update - giải quyết race condition)
                int rowsAffected = await _context.WaitingPatients
                    .Where(w => w.Id == request.WaitingId && w.Status == 0)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.Status, 1)
                        .SetProperty(w => w.DoctorId, doctor.Id)
                        .SetProperty(w => w.AcceptedAt, DateTime.UtcNow));

                if (rowsAffected == 0)
                {
                    return Json(new { success = false, message = "Cảnh báo: Bệnh nhân này vừa được một bác sĩ khác tiếp nhận!" });
                }

                // Gửi email template + QR Check-in ngay khi bác sĩ tiếp nhận thành công
                try
                {
                    await _emailTriggerService.SendDoctorAcceptedCheckInAsync(request.WaitingId, doctor.Id);
                }
                catch (Exception emailEx)
                {
                    // Không chặn luồng tiếp nhận nếu gửi mail lỗi
                    Console.WriteLine($"[AcceptPatient Email] {emailEx.Message}");
                }

                return Json(new { success = true, patientId = waitingPatient.PatientId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tiếp nhận: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetServices()
        {
            var services = await _context.Services
                .Where(s => s.IsActive)
                .Select(s => new { s.Id, s.Name, s.Price, s.Description })
                .ToListAsync();
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

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor == null)
                    return Json(new { success = false, message = "Không tìm thấy hồ sơ bác sĩ." });

                if (request.ServiceIds == null || !request.ServiceIds.Any())
                    return Json(new { success = false, message = "Vui lòng chọn ít nhất một dịch vụ." });

                var services = await _context.Services
                    .Where(s => request.ServiceIds.Contains(s.Id) && s.IsActive)
                    .ToListAsync();

                if (!services.Any())
                    return Json(new { success = false, message = "Các dịch vụ đã chọn không hợp lệ." });

                var payment = new Payment
                {
                    PatientId = request.PatientId,
                    DoctorId = doctor.Id,
                    TotalAmount = services.Sum(s => s.Price),
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                var paymentDetails = services.Select(s => new PaymentDetail
                {
                    PaymentId = payment.Id,
                    ServiceId = s.Id,
                    PriceAtTime = s.Price
                }).ToList();

                _context.PaymentDetails.AddRange(paymentDetails);
                
                // (Removed activeWaiting.Status = 3 here so patient stays in waiting list to be examined)

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã gửi yêu cầu thanh toán thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tạo yêu cầu thanh toán: " + ex.Message });
            }
        }
    }

    public class CreatePaymentRequest
    {
        public int PatientId { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
    }

    public class CancelExamRequest
    {
        public int WaitingId { get; set; }
    }

    public class AcceptPatientRequest
    {
        public int WaitingId { get; set; }
    }
}