using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "Doctor")]
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
                int currentDoctorId = 1; // Fix cứng theo yêu cầu

                var record = new ClinicalRecord
                {
                    PatientId = model.PatientId,
                    DoctorId = currentDoctorId,
                    VisitDate = DateTime.Now, // Dùng giờ Local để hiển thị chuẩn xác
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

                // DỌN DẸP CACHE SAU KHI LƯU DB THÀNH CÔNG
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
