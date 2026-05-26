using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers
{
    public class ClinicalRecordController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public ClinicalRecordController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int id)
        {
            try
            {
                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

                if (patient == null)
                {
                    TempData["Error"] = "Không tìm thấy bệnh nhân.";
                    return RedirectToAction("Index", "DoctorDashboard");
                }

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var records = await _context.ClinicalRecords
                    .Where(r => r.PatientId == id && !r.IsDeleted)
                    .OrderByDescending(r => r.VisitDate)
                    .Select(r => new ClinicalRecordSummaryViewModel
                    {
                        Id = r.Id,
                        VisitDate = r.VisitDate,
                        RestingBP = r.RestingBp,
                        Cholesterol = r.Cholesterol,
                        MaxHeartRate = r.MaxHeartRate,
                        ChestPainTypeDisplay = GetChestPainDisplay(r.ChestPainType),

                        // --- MAP THÊM DỮ LIỆU ---
                        FastingBS = r.FastingBs,
                        RestECG = r.RestEcg,
                        ExerciseAngina = r.ExerciseAngina,
                        OldPeak = r.OldPeak,
                        STSlope = r.Stslope,
                        MajorVessels = r.MajorVessels,
                        ThalResult = r.ThalResult
                    })
                    .ToListAsync();

                var viewModel = new PatientRecordIndexViewModel
                {
                    PatientId = patient.Id,
                    PatientName = patient.User.FullName,
                    Age = today.Year - patient.DateOfBirth.Year - (today.DayOfYear < patient.DateOfBirth.DayOfYear ? 1 : 0),
                    SexDisplay = patient.Sex == 1 ? "Nam" : "Nữ",
                    Records = records
                };

                return View(viewModel);
            }
            catch (Exception)
            {
                TempData["Error"] = "Lỗi khi tải hồ sơ y tế.";
                return RedirectToAction("Index", "DoctorDashboard");
            }
        }

        [HttpGet]
        public IActionResult Create(int patientId)
        {
            var model = new ClinicalRecordFormViewModel { PatientId = patientId };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClinicalRecordFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Lỗi dữ liệu đầu vào: {errors}";
                // Bắn ngược về trang Index để hiển thị lỗi thay vì kẹt ở form
                return RedirectToAction(nameof(Index), new { id = model.PatientId });
            }

            try
            {
                int currentDoctorId = 1;

                var entity = new ClinicalRecord
                {
                    PatientId = model.PatientId,
                    DoctorId = currentDoctorId,
                    VisitDate = DateTime.UtcNow,
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

                _context.ClinicalRecords.Add(entity);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thêm mới hồ sơ y tế thành công.";
                return RedirectToAction(nameof(Index), new { id = model.PatientId });
            }
            catch (Exception)
            {
                TempData["Error"] = "Đã xảy ra lỗi khi lưu dữ liệu.";
                return View(model);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id) // Action này đóng vai trò là Void/Hủy
        {
            try
            {
                // Lấy record cần hủy, không quan tâm nó đã bị xóa hay chưa để kiểm tra null an toàn
                var record = await _context.ClinicalRecords.FirstOrDefaultAsync(r => r.Id == id);

                if (record == null)
                {
                    TempData["Error"] = "Không tìm thấy hồ sơ hệ thống.";
                    return RedirectToAction("Index", "DoctorDashboard");
                }

                if (record.IsDeleted)
                {
                    TempData["Error"] = "Hồ sơ này đã được đánh dấu hủy từ trước.";
                    return RedirectToAction(nameof(Index), new { id = record.PatientId });
                }

                // Chuyển trạng thái sang Soft Delete (Void)
                record.IsDeleted = true;

                // (Optional) Nếu database của bạn có thêm cột VoidedAt hoặc VoidedBy, 
                // đây là nơi lý tưởng để gán giá trị: record.VoidedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã đánh dấu hủy hồ sơ thành công.";
                return RedirectToAction(nameof(Index), new { id = record.PatientId });
            }
            catch (Exception)
            {
                TempData["Error"] = "Lỗi hệ thống khi hủy hồ sơ.";
                return RedirectToAction("Index", "DoctorDashboard");
            }
        }

        private static string GetChestPainDisplay(byte type)
        {
            return type switch
            {
                0 => "Typical Angina (TA)",
                1 => "Atypical Angina (ATA)",
                2 => "Non-Anginal Pain (NAP)",
                3 => "Asymptomatic (ASY)",
                _ => "Unknown"
            };
        }
    }
}
