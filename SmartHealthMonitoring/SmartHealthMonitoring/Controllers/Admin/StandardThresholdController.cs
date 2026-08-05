using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")] // Chỉ Admin (Role = 2)
    [Route("Admin/[controller]/[action]")]
    public class StandardThresholdController : Controller
    {
        private readonly IStandardThresholdService _thresholdService;

        public StandardThresholdController(IStandardThresholdService thresholdService)
        {
            _thresholdService = thresholdService;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _thresholdService.GetAllAsync();
            return View("~/Views/AdminDashboard/StandardThreshold/Index.cshtml", list);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View("~/Views/AdminDashboard/StandardThreshold/Create.cshtml", new StandardThresholdViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StandardThresholdViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/AdminDashboard/StandardThreshold/Create.cshtml", model);

            if (!ValidateThresholdLogic(model))
                return View("~/Views/AdminDashboard/StandardThreshold/Create.cshtml", model);

            var entity = await _thresholdService.CreateAsync(model);
            
            TempData["Success"] = $"Đã tạo ngưỡng chuẩn \"{entity.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _thresholdService.GetByIdAsync(id);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new StandardThresholdViewModel
            {
                Id               = entity.Id,
                Name             = entity.Name,
                Description      = entity.Description,
                Sex              = entity.Sex,
                AgeMin           = entity.AgeMin,
                AgeMax           = entity.AgeMax,
                SystolicBpWarning  = entity.SystolicBpWarning,
                SystolicBpDanger   = entity.SystolicBpDanger,
                DiastolicBpWarning = entity.DiastolicBpWarning,
                DiastolicBpDanger  = entity.DiastolicBpDanger,
                HeartRateWarningMin = entity.HeartRateWarningMin,
                HeartRateDangerMin  = entity.HeartRateDangerMin,
                HeartRateWarningMax = entity.HeartRateWarningMax,
                HeartRateDangerMax  = entity.HeartRateDangerMax,
                IsActive           = entity.IsActive,
                CreatedAt          = entity.CreatedAt,
                UpdatedAt          = entity.UpdatedAt,
            };

            return View("~/Views/AdminDashboard/StandardThreshold/Edit.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StandardThresholdViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/AdminDashboard/StandardThreshold/Edit.cshtml", model);

            if (!ValidateThresholdLogic(model))
                return View("~/Views/AdminDashboard/StandardThreshold/Edit.cshtml", model);

            var entity = await _thresholdService.UpdateAsync(id, model);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Đã cập nhật ngưỡng chuẩn \"{entity.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var entity = await _thresholdService.ToggleActiveAsync(id);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = entity.IsActive
                ? $"Đã kích hoạt lại \"{entity.Name}\"."
                : $"Đã vô hiệu hóa \"{entity.Name}\".";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _thresholdService.DeleteAsync(id);
            if (!result)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Đã xóa ngưỡng chuẩn.";
            return RedirectToAction(nameof(Index));
        }


        private bool ValidateThresholdLogic(StandardThresholdViewModel model)
        {
            bool ok = true;
            if (model.AgeMin > model.AgeMax)
            {
                ModelState.AddModelError("", "Tuổi tối thiểu phải nhỏ hơn hoặc bằng tuổi tối đa.");
                ok = false;
            }
            if (model.SystolicBpWarning >= model.SystolicBpDanger)
            {
                ModelState.AddModelError("", "Ngưỡng Cảnh báo HA Tâm Thu phải nhỏ hơn ngưỡng Nguy hiểm.");
                ok = false;
            }
            if (model.DiastolicBpWarning >= model.DiastolicBpDanger)
            {
                ModelState.AddModelError("", "Ngưỡng Cảnh báo HA Tâm Trương phải nhỏ hơn ngưỡng Nguy hiểm.");
                ok = false;
            }
            if (model.HeartRateDangerMin >= model.HeartRateWarningMin)
            {
                ModelState.AddModelError("", "Ngưỡng Nguy hiểm nhịp tim thấp phải nhỏ hơn Cảnh báo.");
                ok = false;
            }
            if (model.HeartRateWarningMax >= model.HeartRateDangerMax)
            {
                ModelState.AddModelError("", "Ngưỡng Cảnh báo nhịp tim cao phải nhỏ hơn ngưỡng Nguy hiểm.");
                ok = false;
            }
            return ok;
        }
    }
}
