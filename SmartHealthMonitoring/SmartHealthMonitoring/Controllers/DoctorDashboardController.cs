using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
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

        public DoctorDashboardController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            try
            {
                // Lấy thông tin bác sĩ đang đăng nhập để hiển thị trạng thái ca trực
                var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdString, out int userId))
                {
                    var currentDoctor = await _context.Doctors
                        .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);

                    ViewBag.IsOnShift = currentDoctor?.IsOnShift ?? false;

                    // Số cảnh báo chưa xử lý (Status = 0) để hiện banner đỏ khi vừa đăng nhập
                    ViewBag.UnresolvedAlertCount = await _context.WarningAlerts
                        .CountAsync(w => w.Status == 0 && !w.IsDeleted);
                }

                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                // 1. Dựng Query cơ sở
                var query = _context.Patients
                    .Include(p => p.User)
                    .Where(p => !p.IsDeleted && !p.User.IsDeleted && p.User.Role == 0);

                // 2. Đếm tổng số bệnh nhân thỏa mãn điều kiện
                int totalRecords = await query.CountAsync();

                // 3. Thực hiện phân trang và map dữ liệu sang ViewModel
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

                // 4. Đóng gói kết quả
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
                TempData["Error"] = "Lỗi khi tải danh sách bệnh nhân: " + ex.Message;
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
}