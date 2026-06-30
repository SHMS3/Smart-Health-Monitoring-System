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
                    var today = DateTime.UtcNow.Date;

                    // Lấy tất cả thanh toán Paid trong ngày hôm nay của bác sĩ này cho bệnh nhân này
                    var paidPayments = await _context.Payments
                        .Include(p => p.PaymentDetails)
                            .ThenInclude(pd => pd.Service)
                        .Where(p =>
                            p.PatientId == patientId &&
                            p.DoctorId == doctor.Id &&
                            p.Status == "Paid" &&
                            p.CreatedAt >= today)
                        .OrderBy(p => p.PaidAt)
                        .ToListAsync();

                    // Đếm số lượng hồ sơ đã tạo trong ngày hôm nay
                    int recordsCount = await _context.ClinicalRecords
                        .CountAsync(r => r.PatientId == patientId && r.DoctorId == doctor.Id && r.VisitDate >= today && !r.IsDeleted);

                    // 1 lần thanh toán = 1 hồ sơ. Lấy thanh toán chưa được sử dụng
                    Payment? availablePayment = null;
                    if (recordsCount < paidPayments.Count)
                    {
                        availablePayment = paidPayments[recordsCount];
                    }

                    ViewBag.CanFetchData = availablePayment != null;

                    // Lấy danh sách tên dịch vụ đã thanh toán (lowercase để so sánh dễ ở JS)
                    var purchasedServiceNames = availablePayment?.PaymentDetails
                        .Select(pd => pd.Service.Name.ToLower())
                        .ToList() ?? new List<string>();

                    ViewBag.PurchasedServices = purchasedServiceNames;
                }
            }

            // Đảm bảo luôn có giá trị mặc định tránh null ở Razor
            ViewBag.PurchasedServices ??= new List<string>();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicalExamFormViewModel model)
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

            var today = DateTime.UtcNow.Date;
            
            // Lấy tất cả thanh toán Paid trong ngày hôm nay của bác sĩ này cho bệnh nhân này
            var paidPayments = await _context.Payments
                .Include(p => p.PaymentDetails)
                    .ThenInclude(pd => pd.Service)
                .Where(p =>
                    p.PatientId == model.PatientId &&
                    p.DoctorId == doctor.Id &&
                    p.Status == "Paid" &&
                    p.CreatedAt >= today)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            // Đếm số lượng hồ sơ đã tạo trong ngày hôm nay
            int recordsCount = await _context.ClinicalRecords
                .CountAsync(r => r.PatientId == model.PatientId && r.DoctorId == doctor.Id && r.VisitDate >= today && !r.IsDeleted);

            Payment? availablePayment = null;
            if (recordsCount < paidPayments.Count)
            {
                availablePayment = paidPayments[recordsCount];
            }

            var purchasedServiceNames = availablePayment?.PaymentDetails
                .Select(pd => pd.Service.Name.ToLower())
                .ToList() ?? new List<string>();

            bool hasBpPackage = purchasedServiceNames.Any(s => s.Contains("huyết áp"));
            bool hasBloodPackage = purchasedServiceNames.Any(s => s.Contains("huyết học"));
            bool hasEcgPackage = purchasedServiceNames.Any(s => s.Contains("điện tâm đồ") || s.Contains("mạch vành"));

            if (!hasBpPackage)
            {
                ModelState.Remove(nameof(model.ChestPainType));
                ModelState.Remove(nameof(model.ExerciseAngina));
                ModelState.Remove(nameof(model.RestingBP));
                ModelState.Remove(nameof(model.MaxHeartRate));
            }

            if (!hasBloodPackage)
            {
                ModelState.Remove(nameof(model.Cholesterol));
                ModelState.Remove(nameof(model.FastingBS));
            }

            if (!hasEcgPackage)
            {
                ModelState.Remove(nameof(model.RestECG));
                ModelState.Remove(nameof(model.STSlope));
                ModelState.Remove(nameof(model.OldPeak));
                ModelState.Remove(nameof(model.MajorVessels));
                ModelState.Remove(nameof(model.ThalResult));
            }

            // 1. Kiểm tra Lớp 1 (Các ngưỡng Range từ ViewModel)
            if (!ModelState.IsValid)
            {
                ViewBag.CanFetchData = availablePayment != null;
                ViewBag.PurchasedServices = purchasedServiceNames;
                TempData["Error"] = "Hệ thống phát hiện dữ liệu máy đo bất thường. Vui lòng rà soát lại các ô báo đỏ!";
                return View(model);
            }

            // 2. Kiểm tra Lớp 2 (Nghiệp vụ Y khoa chéo)
            // Ví dụ: Bắt ngoại lệ nếu Huyết áp tâm thu < Nhịp tim (Dấu hiệu máy đo hỏng nặng)
            if (hasBpPackage && model.RestingBP < model.MaxHeartRate && model.RestingBP < 80)
            {
                ViewBag.CanFetchData = availablePayment != null;
                ViewBag.PurchasedServices = purchasedServiceNames;
                ModelState.AddModelError("RestingBP", "Ngoại lệ lâm sàng: Huyết áp không thể thấp hơn Nhịp tim tối đa trong trường hợp này. Yêu cầu đo lại!");
                TempData["Error"] = "Cảnh báo: Phát hiện sự bất hợp lý giữa các chỉ số Sinh hiệu!";
                return View(model);
            }

            try
            {

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

                // ── Lấy thông tin patient cho audit log ──────────────────────────────
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == model.PatientId && !p.IsDeleted);


                // ── Lưu ClinicalRecord với NULL cho các gói chưa thanh toán ─────────────────
                // Các chỉ số không được mua giữ nguyên là null (không gán fallback).
                // Fallback chỉ được tính toán ON-THE-FLY bởi AiPredictionService khi phân tích.
                var record = new ClinicalRecord
                {
                    PatientId      = model.PatientId,
                    DoctorId       = doctor.Id,
                    VisitDate      = DateTime.Now,
                    ChestPainType  = model.ChestPainType,   // null nếu gói BP chưa mua
                    RestingBp      = model.RestingBP,       // null nếu gói BP chưa mua
                    Cholesterol    = model.Cholesterol,     // null nếu gói Huyết học chưa mua
                    FastingBs      = model.FastingBS,       // null nếu gói Huyết học chưa mua
                    RestEcg        = model.RestECG,         // null nếu gói ECG chưa mua
                    MaxHeartRate   = model.MaxHeartRate,    // null nếu gói BP chưa mua
                    ExerciseAngina = model.ExerciseAngina,  // null nếu gói BP chưa mua
                    OldPeak        = model.OldPeak,         // null nếu gói ECG chưa mua
                    Stslope        = model.STSlope,         // null nếu gói ECG chưa mua
                    MajorVessels   = model.MajorVessels,    // null nếu gói ECG chưa mua
                    ThalResult     = model.ThalResult,      // null nếu gói ECG chưa mua
                    EcgImageUrl    = model.EcgImageUrl,
                    AttachmentUrl  = model.AttachmentUrl,
                    IsDeleted      = false,
                    IsViewForPatient = model.IsViewForPatient
                };

                _context.ClinicalRecords.Add(record);
                await _context.SaveChangesAsync();

                var activeWaiting = await _context.WaitingPatients
                    .FirstOrDefaultAsync(w => w.PatientId == model.PatientId && (w.Status == 0 || w.Status == 1));
                if (activeWaiting != null)
                {
                    activeWaiting.Status = 3;
                    await _context.SaveChangesAsync();
                }

                _cache.Remove($"LabResult_{model.PatientId}");

                // patient đã được fetch ở trên để tính fallback, dùng lại ở đây

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

        // =============================================
        // HELPERS: Fallback sinh lý theo tuổi & giới
        // Tham chiếu: ACC/AHA 2017, NCEP-ATP III, Haskell-Fox
        // =============================================

        /// <summary>Huyết áp tâm thu nghỉ bình thường (mmHg) theo tuổi và giới tính.</summary>
        private static float GetNormalRestingBP(int age, float sex)
        {
            // sex: 1 = Nam, 0 = Nữ
            return sex >= 1f
                ? age switch { < 30 => 115f, < 40 => 120f, < 50 => 124f, < 60 => 128f, < 70 => 132f, _ => 136f }
                : age switch { < 30 => 110f, < 40 => 114f, < 50 => 118f, < 60 => 128f, < 70 => 134f, _ => 138f };
        }

        /// <summary>
        /// Nhịp tim tối đa lý thuyết khi gắng sức (bpm) theo tuổi và giới tính.
        /// Công thức: Haskell-Fox (220 − age), hiệu chỉnh nữ +5 bpm.
        /// </summary>
        private static float GetNormalMaxHR(int age, float sex)
        {
            float hr = (220f - age) + (sex >= 1f ? 0f : 5f);
            return Math.Clamp(hr, 100f, 200f);
        }

        /// <summary>Cholesterol toàn phần bình thường (mg/dL) theo tuổi và giới tính (NCEP-ATP III).</summary>
        private static float GetNormalCholesterol(int age, float sex)
        {
            return sex >= 1f // Nam
                ? age switch { < 30 => 180f, < 40 => 195f, < 50 => 210f, < 60 => 220f, < 70 => 215f, _ => 210f }
                : age switch { < 30 => 170f, < 40 => 185f, < 50 => 200f, < 60 => 230f, < 70 => 240f, _ => 235f };
        }
    }
}
