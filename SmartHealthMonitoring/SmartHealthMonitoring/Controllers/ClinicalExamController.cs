using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "Doctor")]
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
    }
}
