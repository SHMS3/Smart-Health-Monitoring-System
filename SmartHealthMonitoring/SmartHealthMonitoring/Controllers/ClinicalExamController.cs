
﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "1")] // Chỉ cho phép Bác sĩ (Role = 1) truy cập
    public class ClinicalExamController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;

        public ClinicalExamController(SmartHealthMonitoringContext context, IMemoryCache cache, IEmailService emailService)
        {
            _context = context;
            _cache = cache;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Create(int patientId)
        {
            var model = new ClinicalExamFormViewModel { PatientId = patientId };
            return View(model);
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        
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

                // GỬI EMAIL TỰ ĐỘNG
                try
                {
                    var patient = await _context.Patients
                        .Include(p => p.User)
                        .FirstOrDefaultAsync(p => p.Id == model.PatientId);

                    if (patient != null && patient.User != null && !string.IsNullOrEmpty(patient.User.Email))
                    {
                        var replacements = new Dictionary<string, string>
                        {
                            { "{{PatientName}}", patient.User.FullName },
                            { "{{RecordDate}}", record.VisitDate.ToString("dd/MM/yyyy HH:mm") },
                            { "{{Diagnosis}}", "Kết quả khám lâm sàng" }, 
                            { "{{Severity}}", "Bình thường" },
                            { "{{VitalSigns}}", $"Nhịp tim Max: {record.MaxHeartRate} bpm | Huyết áp tĩnh: {record.RestingBp} mm/Hg" },
                            { "{{DoctorAdvice}}", "Các chỉ số hiện tại đã được ghi nhận. Vui lòng theo dõi hoặc liên hệ trực tiếp bác sĩ nếu có dấu hiệu mệt mỏi, khó thở bất thường." }
                        };

                        string htmlContent = _emailService.GetHtmlContentFromFile("PatientHealthReportTemplate.html", replacements);
                        if (!string.IsNullOrEmpty(htmlContent))
                        {
                            await _emailService.SendEmailAsync(patient.User.Email, "Báo cáo Tình trạng Y tế - Smart Health", htmlContent);
                        }
                    }
                }
                catch (Exception emailEx)
                {
                    // Catch và bỏ qua để lỗi gửi mail không làm crash luồng lưu dữ liệu y tế
                    Console.WriteLine($"Lỗi khi gửi email sau khi lưu: {emailEx.Message}");
                }

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

        // =============================================
        // FEATURE: CẤU HÌNH NGƯỠNG CHO BỆNH NHÂN
        // =============================================

        [HttpGet]
        public async Task<IActionResult> SettingPatientThreshold(int patientId)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .Include(p => p.PatientThreshold)
                    .ThenInclude(t => t!.UpdatedByDoctor)
                        .ThenInclude(d => d!.User)
                .FirstOrDefaultAsync(p => p.Id == patientId && !p.IsDeleted);

            if (patient == null)
            {
                TempData["Error"] = "Không tìm thấy bệnh nhân.";
                return RedirectToAction("Index", "DoctorDashboard");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            int age = today.Year - patient.DateOfBirth.Year
                      - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

            var existing = patient.PatientThreshold;

            var vm = new PatientThresholdViewModel
            {
                PatientId       = patient.Id,
                PatientName     = patient.User.FullName,
                Age             = age,
                SexDisplay      = patient.Sex == 1 ? "Nam" : "Nữ",
                IsConfigured    = existing != null,
                ThresholdId     = existing?.Id,
                LastUpdatedAt   = existing?.UpdatedAt,
                LastUpdatedByDoctorId      = existing?.UpdatedByDoctorId,
                LastUpdatedByDoctor        = existing?.UpdatedByDoctor?.User?.FullName,
                LastUpdatedByDoctorSpecialty = existing?.UpdatedByDoctor?.Specialty,

                // Dùng giá trị đã cấu hình, nếu chưa có thì lấy default từ model
                SystolicBpWarning  = existing?.SystolicBpWarning  ?? 130,
                SystolicBpDanger   = existing?.SystolicBpDanger   ?? 140,
                DiastolicBpWarning = existing?.DiastolicBpWarning ?? 80,
                DiastolicBpDanger  = existing?.DiastolicBpDanger  ?? 90,
                HeartRateWarningMin = existing?.HeartRateWarningMin ?? 60,
                HeartRateDangerMin  = existing?.HeartRateDangerMin  ?? 50,
                HeartRateWarningMax = existing?.HeartRateWarningMax ?? 100,
                HeartRateDangerMax  = existing?.HeartRateDangerMax  ?? 120,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SettingPatientThreshold(PatientThresholdViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validate logic: Warning phải nhỏ hơn Danger
            if (model.SystolicBpWarning >= model.SystolicBpDanger)
            {
                ModelState.AddModelError("", "Ngưỡng Cảnh báo Huyết áp TT phải nhỏ hơn ngưỡng Nguy hiểm.");
                return View(model);
            }
            if (model.DiastolicBpWarning >= model.DiastolicBpDanger)
            {
                ModelState.AddModelError("", "Ngưỡng Cảnh báo Huyết áp TR phải nhỏ hơn ngưỡng Nguy hiểm.");
                return View(model);
            }
            if (model.HeartRateDangerMin >= model.HeartRateWarningMin)
            {
                ModelState.AddModelError("", "Ngưỡng Nguy hiểm Nhịp tim thấp phải nhỏ hơn ngưỡng Cảnh báo.");
                return View(model);
            }
            if (model.HeartRateWarningMax >= model.HeartRateDangerMax)
            {
                ModelState.AddModelError("", "Ngưỡng Cảnh báo Nhịp tim cao phải nhỏ hơn ngưỡng Nguy hiểm.");
                return View(model);
            }

            // Lấy doctorId từ claim
            int? doctorId = null;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
                doctorId = doctor?.Id;
            }

            // UPSERT: Nếu đã có thì cập nhật, chưa có thì tạo mới
            var existing = await _context.PatientThresholds
                .FirstOrDefaultAsync(t => t.PatientId == model.PatientId);

            if (existing == null)
            {
                var newThreshold = new PatientThreshold
                {
                    PatientId          = model.PatientId,
                    SystolicBpWarning  = model.SystolicBpWarning,
                    SystolicBpDanger   = model.SystolicBpDanger,
                    DiastolicBpWarning = model.DiastolicBpWarning,
                    DiastolicBpDanger  = model.DiastolicBpDanger,
                    HeartRateWarningMin = model.HeartRateWarningMin,
                    HeartRateDangerMin  = model.HeartRateDangerMin,
                    HeartRateWarningMax = model.HeartRateWarningMax,
                    HeartRateDangerMax  = model.HeartRateDangerMax,
                    UpdatedAt          = DateTime.Now,
                    UpdatedByDoctorId  = doctorId
                };
                _context.PatientThresholds.Add(newThreshold);
            }
            else
            {
                existing.SystolicBpWarning  = model.SystolicBpWarning;
                existing.SystolicBpDanger   = model.SystolicBpDanger;
                existing.DiastolicBpWarning = model.DiastolicBpWarning;
                existing.DiastolicBpDanger  = model.DiastolicBpDanger;
                existing.HeartRateWarningMin = model.HeartRateWarningMin;
                existing.HeartRateDangerMin  = model.HeartRateDangerMin;
                existing.HeartRateWarningMax = model.HeartRateWarningMax;
                existing.HeartRateDangerMax  = model.HeartRateDangerMax;
                existing.UpdatedAt          = DateTime.Now;
                existing.UpdatedByDoctorId  = doctorId;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã lưu cấu hình ngưỡng cho bệnh nhân thành công!";
            return RedirectToAction("Index", "ClinicalRecord", new { id = model.PatientId });
        }
    }
}