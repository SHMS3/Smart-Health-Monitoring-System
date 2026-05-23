//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using SmartHealthMonitoring.Context;
//using SmartHealthMonitoring.Models;
//using SmartHealthMonitoring.Services;
//using SmartHealthMonitoring.ViewModels;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading.Tasks;

//namespace SmartHealthMonitoring.Controllers
//{
//    public class DoctorDashboardController : Controller
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IMinioService _minioService;

//        public DoctorDashboardController(ApplicationDbContext context, IMinioService minioService)
//        {
//            _context = context;
//            _minioService = minioService;
//        }

//        // 1. TRANG DANH SÁCH BỆNH NHÂN
//        [HttpGet]
//        public async Task<IActionResult> Index()
//        {
//            var patients = await _context.Patients
//                .Where(p => !p.IsDeleted)
//                .Select(p => new PatientListDto
//                {
//                    PatientId = p.PatientId,
//                    FullName = p.FullName,
//                    Age = DateTime.Today.Year - p.DateOfBirth.Year,
//                    Gender = p.Gender,
//                    PhoneNumber = p.PhoneNumber
//                })
//                .ToListAsync();

//            return View(patients);
//        }

//        // 2. CHI TIẾT HỒ SƠ SỨC KHỎE & DANH SÁCH XÉT NGHIỆM MINIO
//        [HttpGet]
//        public async Task<IActionResult> PatientHealth(Guid? patientId)
//        {
//            if (patientId == null || patientId == Guid.Empty)
//            {
//                return BadRequest("Không tìm thấy mã Bệnh nhân!");
//            }

//            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientId == patientId);
//            if (patient == null)
//            {
//                return NotFound("Không tìm thấy hồ sơ bệnh nhân trong hệ thống!");
//            }

//            int age = DateTime.Today.Year - patient.DateOfBirth.Year;

//            // Lấy lịch sử đo chỉ số sinh tồn
//            var metrics = await _context.HealthMetrics
//                .Include(h => h.MetricType)
//                .Where(h => h.PatientId == patientId)
//                .OrderByDescending(h => h.MeasuredAt)
//                .Select(h => new HealthMetricHistoryDto
//                {
//                    MetricId = h.MetricId,
//                    MetricName = h.MetricType.Name,
//                    Unit = h.MetricType.Unit,
//                    Value = h.Value,
//                    MeasuredAt = h.MeasuredAt,
//                    Notes = h.Notes
//                })
//                .ToListAsync();

//            // XỬ LÝ TỰ ĐỘNG: Lấy hồ sơ bệnh án (MedicalRecord), nếu chưa có thì tự động tạo mồi
//            var medicalRecord = await _context.MedicalRecords
//                .OrderByDescending(m => m.CreatedAt)
//                .FirstOrDefaultAsync(m => m.PatientId == patientId && !m.IsDeleted);

//            if (medicalRecord == null)
//            {
//                // Tìm tài khoản bác sĩ đầu tiên trong DB để gán trách nhiệm hồ sơ
//                var firstDoctor = await _context.Doctors.FirstOrDefaultAsync();

//                if (firstDoctor != null)
//                {
//                    medicalRecord = new MedicalRecord
//                    {
//                        RecordId = Guid.NewGuid(),
//                        PatientId = patient.PatientId,
//                        DoctorId = firstDoctor.DoctorId,
//                        Status = "Open",
//                        IsDeleted = false,
//                        CreatedAt = DateTime.Now
//                    };
//                    _context.MedicalRecords.Add(medicalRecord);
//                    await _context.SaveChangesAsync();
//                }
//            }

//            Guid currentRecordId = medicalRecord?.RecordId ?? Guid.Empty;

//            // Lấy list xét nghiệm cận lâm sàng liên kết với RecordId này
//            var labsFromDb = await _context.LabResults
//                .Where(l => l.RecordId == currentRecordId)
//                .OrderByDescending(l => l.UploadedAt)
//                .ToListAsync();

//            // Đọc tên file từ DB và sinh link bảo mật chữ ký (Presigned URL) từ MinIO
//            var labResultsDto = new List<LabResultDto>();
//            foreach (var lab in labsFromDb)
//            {
//                string secureUrl = string.Empty;
//                if (!string.IsNullOrEmpty(lab.ResultFileUrl))
//                {
//                    // Sinh URL có thời hạn sử dụng trong 15 phút
//                    secureUrl = await _minioService.GetPresignedUrlAsync("lab-results", lab.ResultFileUrl, 15);
//                }

//                labResultsDto.Add(new LabResultDto
//                {
//                    LabId = lab.LabId,
//                    TestName = lab.TestName,
//                    UploadedAt = lab.UploadedAt ?? DateTime.MinValue, // Xử lý lỗi Nullable DateTime
//                    FileUrl = secureUrl
//                });
//            }

//            // Gán toàn bộ vào ViewModel tổng để render ra giao diện
//            var viewModel = new PatientDashboardViewModel
//            {
//                PatientId = patient.PatientId,
//                FullName = patient.FullName,
//                Age = age,
//                Gender = patient.Gender,
//                BloodType = patient.BloodType,
//                PhoneNumber = patient.PhoneNumber,
//                MetricsHistory = metrics,
//                CurrentRecordId = currentRecordId,
//                LabResults = labResultsDto
//            };

//            return View(viewModel);
//        }

//        // 3. ĐẨY FILE NHỊ PHÂN LÊN BUCKET MINIO
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> UploadLabResult(Guid patientId, Guid recordId, string testName, IFormFile uploadFile)
//        {
//            if (recordId == Guid.Empty)
//            {
//                TempData["ErrorMessage"] = "Không thể tải file! Bệnh nhân này chưa được khởi tạo Hồ sơ bệnh án trong hệ thống.";
//                return RedirectToAction(nameof(PatientHealth), new { patientId = patientId });
//            }

//            if (uploadFile != null && uploadFile.Length > 0)
//            {
//                try
//                {
//                    // Tạo tên object độc nhất dạng GUID để lưu trên MinIO Cloud Storage
//                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(uploadFile.FileName)}";

//                    // Đọc trực tiếp luồng stream từ file được up lên
//                    using (var stream = uploadFile.OpenReadStream())
//                    {
//                        await _minioService.UploadFileAsync("lab-results", uniqueFileName, stream, uploadFile.ContentType);
//                    }

//                    // Lưu bản ghi vào SQL Server, gán chuỗi GUID tên file vào cột ResultFileUrl
//                    var lab = new LabResult
//                    {
//                        LabId = Guid.NewGuid(),
//                        RecordId = recordId,
//                        TestName = testName,
//                        ResultFileUrl = uniqueFileName,
//                        UploadedAt = DateTime.Now
//                    };

//                    _context.LabResults.Add(lab);
//                    await _context.SaveChangesAsync();

//                    TempData["SuccessMessage"] = "Đã tải kết quả xét nghiệm lên hệ thống lưu trữ MinIO thành công!";
//                }
//                catch (Exception ex)
//                {
//                    TempData["ErrorMessage"] = $"Lỗi hệ thống MinIO: {ex.Message}";
//                }
//            }
//            else
//            {
//                TempData["ErrorMessage"] = "Vui lòng chọn một file hợp lệ trước khi bấm nút tải lên!";
//            }

//            return RedirectToAction(nameof(PatientHealth), new { patientId = patientId });
//        }
//    }
//}