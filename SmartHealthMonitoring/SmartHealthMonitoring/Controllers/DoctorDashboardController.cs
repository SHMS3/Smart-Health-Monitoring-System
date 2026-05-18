using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers
{
    public class DoctorDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DoctorDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Truy vấn lấy toàn bộ bệnh nhân trong hệ thống
            var patients = await _context.Patients
                .Select(p => new PatientListDto
                {
                    PatientId = p.PatientId,
                    FullName = p.FullName,
                    Age = DateTime.Today.Year - p.DateOfBirth.Year, // Tính tuổi
                    Gender = p.Gender,
                    PhoneNumber = p.PhoneNumber
                })
                .ToListAsync();

            return View(patients);
        }

        // Action xem Dashboard của một Bệnh nhân cụ thể
        // URL test: /DoctorDashboard/PatientHealth?patientId=MÃ_ID_CỦA_BỆNH_NHÂN
        [HttpGet]
        public async Task<IActionResult> PatientHealth(Guid? patientId)
        {
            // 1. Truy vấn thông tin bệnh nhân
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.PatientId == patientId);

            if (patient == null)
            {
                return NotFound("Không tìm thấy hồ sơ bệnh nhân trong hệ thống!");
            }

            // 2. Tính tuổi
            int age = DateTime.Today.Year - patient.DateOfBirth.Year;

            // 3. Lấy lịch sử đo sức khỏe
            var metrics = await _context.HealthMetrics
                .Include(h => h.MetricType)
                .Where(h => h.PatientId == patientId)
                .OrderByDescending(h => h.MeasuredAt)
                .Select(h => new HealthMetricHistoryDto
                {
                    MetricId = h.MetricId,
                    MetricName = h.MetricType.Name,
                    Unit = h.MetricType.Unit,
                    Value = h.Value,
                    MeasuredAt = h.MeasuredAt,
                    Notes = h.Notes
                })
                .ToListAsync();

            // 4. Map dữ liệu ra ViewModel
            var viewModel = new PatientDashboardViewModel
            {
                PatientId = patient.PatientId,
                FullName = patient.FullName,
                Age = age,
                Gender = patient.Gender,
                BloodType = patient.BloodType,
                PhoneNumber = patient.PhoneNumber,
                MetricsHistory = metrics
            };

            return View(viewModel);
        }
    }
}
