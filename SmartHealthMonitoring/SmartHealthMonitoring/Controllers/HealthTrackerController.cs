//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.EntityFrameworkCore;
//using SmartHealthMonitoring.Context;
//using SmartHealthMonitoring.Models;
//using SmartHealthMonitoring.ViewModels;


//namespace SmartHealthMonitoring.Controllers
//{
//    public class HealthTrackerController : Controller
//    {
//        private readonly ApplicationDbContext _context;

//        public HealthTrackerController(ApplicationDbContext context)
//        {
//            _context = context;
//        }

//        [HttpGet]
//        public async Task<IActionResult> Index()
//        {
//            Guid currentPatientId = Guid.Parse("DC6730DD-9E2D-4759-B08B-2B916CD54F2D"); // ID test, nhớ tạo 1 cái ở db rồi thay vào đây

//            var viewModel = new HealthTrackerViewModel();

//            var metricTypes = await _context.MetricTypes.ToListAsync();
//            viewModel.AvailableMetrics = metricTypes.Select(m => new SelectListItem
//            {
//                Value = m.MetricTypeId.ToString(),
//                Text = $"{m.Name} ({m.Unit})"
//            }).ToList();

//            viewModel.History = await _context.HealthMetrics
//                .Include(h => h.MetricType)
//                .Where(h => h.PatientId == currentPatientId)
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

//            return View(viewModel);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> CreateMetric(HealthTrackerViewModel model)
//        {
//            Guid currentPatientId = Guid.Parse("DC6730DD-9E2D-4759-B08B-2B916CD54F2D"); // Đổi ID test khớp với ID bên trên

//            if (model.MetricTypeId == 2 && model.Value > 300)
//            {
//                ModelState.AddModelError("Value", "Nhịp tim đo được quá cao, không hợp lệ.");
//            }

//            if (ModelState.IsValid)
//            {
//                // 3. SỬA KHỞI TẠO TỪ HealthMetrics THÀNH HealthMetric (số ít)
//                var newMetric = new HealthMetric
//                {
//                    PatientId = currentPatientId,
//                    MetricTypeId = model.MetricTypeId,
//                    Value = model.Value,
//                    Notes = model.Notes,
//                    // Bỏ MeasuredAt vì DB của bạn đã set default là (getdate())
//                    Source = "Manual"
//                };

//                _context.HealthMetrics.Add(newMetric);
//                await _context.SaveChangesAsync();

//                TempData["SuccessMessage"] = "Đã lưu chỉ số sức khỏe thành công!";
//                return RedirectToAction(nameof(Index));
//            }

//            return RedirectToAction(nameof(Index));
//        }

//        [HttpPost]
//        public async Task<IActionResult> DeleteMetric(Guid id)
//        {
//            var metric = await _context.HealthMetrics.FindAsync(id);
//            if (metric != null)
//            {
//                _context.HealthMetrics.Remove(metric);
//                await _context.SaveChangesAsync();
//                TempData["SuccessMessage"] = "Đã xóa bản ghi bị lỗi thành công!";
//            }
//            return RedirectToAction(nameof(Index));
//        }
//    }
//}
