using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
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
        private readonly IMinioService _minioService;
        private readonly IAuditLogService _auditLogService;

        public ClinicalExamController(
            SmartHealthMonitoringContext context,
            IMemoryCache cache,
            IEmailService emailService,
            IMinioService minioService,
            IAuditLogService auditLogService)
        {
            _context = context;
            _cache = cache;
            _emailService = emailService;
            _minioService = minioService;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Create(int patientId)
        {
            var model = new ClinicalExamFormViewModel { PatientId = patientId };
            
            // Lấy id bác sĩ
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
                if (doctor != null)
                {
                    // Check if there is a Paid payment for this patient and doctor today
                    var today = DateTime.UtcNow.Date;
                    var hasPaidPayment = await _context.Payments.AnyAsync(p => 
                        p.PatientId == patientId && 
                        p.DoctorId == doctor.Id && 
                        p.Status == "Paid" && 
                        p.CreatedAt >= today);

                    ViewBag.CanFetchData = hasPaidPayment;
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicalExamFormViewModel model)
        {
            // 1. Kiểm tra Lớp 1 (Các ngưỡng Range từ ViewModel)
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Hệ thống phát hiện dữ liệu máy đo bất thường. Vui lòng rà soát lại các ô báo đỏ!";
                return View(model);
            }

            // 2. Kiểm tra Lớp 2 (Nghiệp vụ Y khoa chéo)
            // Ví dụ: Bắt ngoại lệ nếu Huyết áp tâm thu < Nhịp tim (Dấu hiệu máy đo hỏng nặng)
            if (model.RestingBP < model.MaxHeartRate && model.RestingBP < 80)
            {
                ModelState.AddModelError("RestingBP", "Ngoại lệ lâm sàng: Huyết áp không thể thấp hơn Nhịp tim tối đa trong trường hợp này. Yêu cầu đo lại!");
                TempData["Error"] = "Cảnh báo: Phát hiện sự bất hợp lý giữa các chỉ số Sinh hiệu!";
                return View(model);
            }

            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    TempData["Error"] = "Không thể xác thực danh tính. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Login", "Auth");
                }

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);

                if (doctor == null)
                {
                    TempData["Error"] = "Tài khoản của bạn không có hồ sơ Bác sĩ hợp lệ.";
                    return RedirectToAction("Index", "Home");
                }

                if (model.AttachmentFile != null && model.AttachmentFile.Length > 0)
                {
                    using (var stream = model.AttachmentFile.OpenReadStream())
                    {
                        // Đặt tên file: attach_PatientId_Timestamp_TenFileGoc.ext
                        string extension = Path.GetExtension(model.AttachmentFile.FileName);
                        string objectName = $"attach_{model.PatientId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{extension}";
                        string bucketName = "clinical-attachments";

                        // Đẩy file lên MinIO
                        await _minioService.UploadFileAsync(bucketName, objectName, stream, model.AttachmentFile.ContentType);

                        // Sinh link bảo mật thời hạn 7 ngày
                        model.AttachmentUrl = await _minioService.GetPresignedUrlAsync(bucketName, objectName, 10080);
                    }
                }

                var record = new ClinicalRecord
                {
                    PatientId = model.PatientId,
                    DoctorId = doctor.Id,
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
                    EcgImageUrl = model.EcgImageUrl,
                    AttachmentUrl = model.AttachmentUrl,
                    IsDeleted = false,
                    IsViewForPatient = model.IsViewForPatient
                };

                _context.ClinicalRecords.Add(record);
                await _context.SaveChangesAsync();

                _cache.Remove($"LabResult_{model.PatientId}");

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == model.PatientId);

                await _auditLogService.LogAsync(
                    "Create",
                    "ClinicalRecord",
                    record.Id.ToString(),
                    $"Tạo hồ sơ lâm sàng #{record.Id} cho bệnh nhân {patient?.User?.FullName ?? $"#{model.PatientId}"}; huyết áp {record.RestingBp}, cholesterol {record.Cholesterol}, nhịp tim tối đa {record.MaxHeartRate}.",
                    patient?.UserId,
                    patient?.User?.FullName);

                // ĐÃ TẮT CƠ CHẾ GỬI EMAIL TỰ ĐỘNG THEO YÊU CẦU NGHIỆP VỤ Y KHOA
                // Tránh tình trạng bệnh nhân nhận kết quả chẩn đoán bệnh hiểm nghèo qua email mà không có Bác sĩ tư vấn tâm lý.
                /*
                try
                {
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
                            // Đẩy việc gửi mail chạy ngầm (Fire and Forget) để không làm chậm UI
                            var userEmail = patient.User.Email;
                            _ = Task.Run(async () => 
                            {
                                await _emailService.SendEmailAsync(userEmail, "Báo cáo Tình trạng Y tế - Smart Health", htmlContent);
                            });
                        }
                    }
                }
                catch (Exception emailEx)
                {
                    Console.WriteLine($"Lỗi khi gửi email sau khi lưu: {emailEx.Message}");
                }
                */

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
        // FEATURE: CẤU HÌNH NGƯỠNG CHO BỆNH NHÂN (Giữ nguyên)
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
            int age = today.Year - patient.DateOfBirth.Year - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0);

            var existing = patient.PatientThreshold;

            var vm = new PatientThresholdViewModel
            {

                PatientId = patient.Id,
                PatientName = patient.User.FullName,
                Age = age,
                SexDisplay = patient.Sex == 1 ? "Nam" : "Nữ",
                IsConfigured = existing != null,
                ThresholdId = existing?.Id,
                LastUpdatedAt = existing?.UpdatedAt,
                LastUpdatedByDoctorId = existing?.UpdatedByDoctorId,
                LastUpdatedByDoctor = existing?.UpdatedByDoctor?.User?.FullName,
                LastUpdatedByDoctorSpecialty = existing?.UpdatedByDoctor?.Specialty,

                SystolicBpWarning = existing?.SystolicBpWarning ?? 130,
                SystolicBpDanger = existing?.SystolicBpDanger ?? 140,
                DiastolicBpWarning = existing?.DiastolicBpWarning ?? 80,
                DiastolicBpDanger = existing?.DiastolicBpDanger ?? 90,
                HeartRateWarningMin = existing?.HeartRateWarningMin ?? 60,
                HeartRateDangerMin = existing?.HeartRateDangerMin ?? 50,
                HeartRateWarningMax = existing?.HeartRateWarningMax ?? 100,
                HeartRateDangerMax = existing?.HeartRateDangerMax ?? 120,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SettingPatientThreshold(PatientThresholdViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

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

            int? doctorId = null;
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId);
                doctorId = doctor?.Id;
            }

            var existing = await _context.PatientThresholds.FirstOrDefaultAsync(t => t.PatientId == model.PatientId);
            var isNewThreshold = existing == null;
            PatientThreshold threshold;

            if (existing == null)
            {
                threshold = new PatientThreshold
                {
                    PatientId = model.PatientId,
                    SystolicBpWarning = model.SystolicBpWarning,
                    SystolicBpDanger = model.SystolicBpDanger,
                    DiastolicBpWarning = model.DiastolicBpWarning,
                    DiastolicBpDanger = model.DiastolicBpDanger,
                    HeartRateWarningMin = model.HeartRateWarningMin,
                    HeartRateDangerMin = model.HeartRateDangerMin,
                    HeartRateWarningMax = model.HeartRateWarningMax,
                    HeartRateDangerMax = model.HeartRateDangerMax,
                    UpdatedAt = DateTime.Now,
                    UpdatedByDoctorId = doctorId
                };
                _context.PatientThresholds.Add(threshold);
            }
            else
            {
                threshold = existing;
                threshold.SystolicBpWarning = model.SystolicBpWarning;
                threshold.SystolicBpDanger = model.SystolicBpDanger;
                threshold.DiastolicBpWarning = model.DiastolicBpWarning;
                threshold.DiastolicBpDanger = model.DiastolicBpDanger;
                threshold.HeartRateWarningMin = model.HeartRateWarningMin;
                threshold.HeartRateDangerMin = model.HeartRateDangerMin;
                threshold.HeartRateWarningMax = model.HeartRateWarningMax;
                threshold.HeartRateDangerMax = model.HeartRateDangerMax;
                threshold.UpdatedAt = DateTime.Now;
                threshold.UpdatedByDoctorId = doctorId;
            }

            await _context.SaveChangesAsync();

            var patientForAudit = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == model.PatientId);

            await _auditLogService.LogAsync(
                isNewThreshold ? "Create" : "Update",
                "PatientThreshold",
                threshold.Id.ToString(),
                $"{(isNewThreshold ? "Tạo" : "Cập nhật")} ngưỡng riêng cho bệnh nhân {patientForAudit?.User?.FullName ?? $"#{model.PatientId}"}; huyết áp tâm thu {threshold.SystolicBpWarning}/{threshold.SystolicBpDanger}, nhịp tim {threshold.HeartRateWarningMin}-{threshold.HeartRateWarningMax}.",
                patientForAudit?.UserId,
                patientForAudit?.User?.FullName);

            TempData["Success"] = $"Đã lưu cấu hình ngưỡng cho bệnh nhân thành công!";
            return RedirectToAction("Index", "ClinicalRecord", new { id = model.PatientId });
        }

        // =============================================
        // API: GỢI Ý NGƯỠNG CHUẨN CHO BÁC SĨ
        // =============================================

        /// <summary>
        /// Trả về ngưỡng chuẩn phù hợp nhất theo giới tính và độ tuổi.
        /// GET /ClinicalExam/GetSuggestedThreshold?sex=1&age=45
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSuggestedThreshold(byte sex, int age)
        {
            // Ưu tiên: khớp đúng giới tính + tuổi nằm trong khoảng → nếu không có thì lấy template "Chung"
            var templates = await _context.StandardThresholds
                .Where(t => t.IsActive && age >= t.AgeMin && age <= t.AgeMax)
                .ToListAsync();

            // Tìm template khớp giới tính chính xác trước
            var matched = templates.FirstOrDefault(t => t.Sex == sex)
                       ?? templates.FirstOrDefault(t => t.Sex == 2); // fallback: chung

            if (matched == null)
                return Json(new { success = false, message = "Không tìm thấy ngưỡng chuẩn phù hợp." });

            return Json(new
            {
                success = true,
                templateId   = matched.Id,
                templateName = matched.Name,
                description  = matched.Description,
                systolicBpWarning   = matched.SystolicBpWarning,
                systolicBpDanger    = matched.SystolicBpDanger,
                diastolicBpWarning  = matched.DiastolicBpWarning,
                diastolicBpDanger   = matched.DiastolicBpDanger,
                heartRateWarningMin = matched.HeartRateWarningMin,
                heartRateDangerMin  = matched.HeartRateDangerMin,
                heartRateWarningMax = matched.HeartRateWarningMax,
                heartRateDangerMax  = matched.HeartRateDangerMax,
            });
        }

        /// <summary>
        /// Trả về tất cả ngưỡng chuẩn đang active để bác sĩ chọn từ dropdown.
        /// GET /ClinicalExam/GetAllStandardThresholds
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllStandardThresholds()
        {
            var list = await _context.StandardThresholds
                .Where(t => t.IsActive)
                .OrderBy(t => t.Sex)
                .ThenBy(t => t.AgeMin)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Description,
                    t.Sex,
                    t.AgeMin,
                    t.AgeMax,
                    t.SystolicBpWarning,
                    t.SystolicBpDanger,
                    t.DiastolicBpWarning,
                    t.DiastolicBpDanger,
                    t.HeartRateWarningMin,
                    t.HeartRateDangerMin,
                    t.HeartRateWarningMax,
                    t.HeartRateDangerMax,
                })
                .ToListAsync();

            return Json(list);
        }
    }
}
