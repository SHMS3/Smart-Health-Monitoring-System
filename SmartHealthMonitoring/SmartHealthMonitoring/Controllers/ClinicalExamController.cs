using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")] // Chỉ cho phép Bác sĩ (Role = 1) truy cập
    public class ClinicalExamController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IMemoryCache _cache;

        public ClinicalExamController(SmartHealthMonitoringContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public IActionResult Create(int patientId)
        {
            var model = new ClinicalExamFormViewModel { PatientId = patientId };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicalExamFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ. Vui lòng kiểm tra lại các trường.";
                return View(model);
            }

            try
            {
                // 1. Lấy UserId từ Cookie Đăng nhập (Claims)
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    TempData["Error"] = "Không thể xác thực danh tính. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Auth");
                }

                // 2. Tìm DoctorId tương ứng với UserId trong bảng Doctors
                var doctor = await _context.Doctors
                    .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);

                if (doctor == null)
                {
                    TempData["Error"] = "Tài khoản của bạn không có hồ sơ Bác sĩ hợp lệ.";
                    return RedirectToAction("Index", "Home");
                }

                // 3. Khởi tạo bản ghi và gán DoctorId linh động
                var record = new ClinicalRecord
                {
                    PatientId = model.PatientId,
                    DoctorId = doctor.Id, // ĐÃ FIX: Lấy linh động từ Database
                    VisitDate = DateTime.Now,
                    ChestPainType = model.ChestPainType,
                    RestingBp = model.RestingBP,
                    Cholesterol = model.Cholesterol,
                    FastingBs = model.FastingBS,
                    RestEcg = model.RestECG,
                    MaxHeartRate = model.MaxHeartRate,
                    ExerciseAngina = model.ExerciseAngina,
                    OldPeak = model.OldPeak,
                    Stslope = model.STSlope,
                    MajorVessels = model.MajorVessels,
                    ThalResult = model.ThalResult,
                    IsDeleted = false
                };

                _context.ClinicalRecords.Add(record);
                await _context.SaveChangesAsync();

                // 4. DỌN DẸP CACHE SAU KHI LƯU DB THÀNH CÔNG
                _cache.Remove($"LabResult_{model.PatientId}");

                TempData["Success"] = "Đã lưu phiếu khám lâm sàng thành công.";
                return RedirectToAction("Index", "ClinicalRecord", new { id = model.PatientId });
            }
            catch (Exception ex)
            {
                string dbError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                TempData["Error"] = $"Lỗi hệ thống: {dbError}";
                return View(model);
            }
        }
    }
}