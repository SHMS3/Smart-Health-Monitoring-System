using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")] // Chỉ Admin (Role = 2)
    [Route("Admin/[controller]/[action]")]
    public class StandardThresholdController : Controller
    {
        private readonly SmartHealthMonitoringContext _context;

        public StandardThresholdController(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        // GET: /Admin/StandardThreshold/Index
        public async Task<IActionResult> Index()
        {
            var list = await _context.StandardThresholds
                .OrderBy(t => t.Sex)
                .ThenBy(t => t.AgeMin)
                .ToListAsync();

            return View("~/Views/AdminDashboard/StandardThreshold/Index.cshtml", list);
        }

        // GET: /Admin/StandardThreshold/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View("~/Views/AdminDashboard/StandardThreshold/Create.cshtml", new StandardThresholdViewModel());
        }

        // POST: /Admin/StandardThreshold/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StandardThresholdViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/AdminDashboard/StandardThreshold/Create.cshtml", model);

            if (!ValidateThresholdLogic(model))
                return View("~/Views/AdminDashboard/StandardThreshold/Create.cshtml", model);

            var entity = MapToEntity(model, new StandardThreshold());
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            _context.StandardThresholds.Add(entity);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo ngưỡng chuẩn \"{entity.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/StandardThreshold/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            return View("~/Views/AdminDashboard/StandardThreshold/Edit.cshtml", MapToViewModel(entity));
        }

        // POST: /Admin/StandardThreshold/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StandardThresholdViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/AdminDashboard/StandardThreshold/Edit.cshtml", model);

            if (!ValidateThresholdLogic(model))
                return View("~/Views/AdminDashboard/StandardThreshold/Edit.cshtml", model);

            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            MapToEntity(model, entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật ngưỡng chuẩn \"{entity.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/StandardThreshold/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            entity.IsActive = !entity.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = entity.IsActive
                ? $"Đã kích hoạt lại \"{entity.Name}\"."
                : $"Đã vô hiệu hóa \"{entity.Name}\".";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/StandardThreshold/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.StandardThresholds.FindAsync(id);
            if (entity == null)
            {
                TempData["Error"] = "Không tìm thấy ngưỡng chuẩn.";
                return RedirectToAction(nameof(Index));
            }

            _context.StandardThresholds.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa ngưỡng chuẩn \"{entity.Name}\".";
            return RedirectToAction(nameof(Index));
        }

        // =============================================
        // HELPERS
        // =============================================

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

        private static StandardThreshold MapToEntity(StandardThresholdViewModel vm, StandardThreshold entity)
        {
            entity.Name              = vm.Name;
            entity.Description       = vm.Description;
            entity.Sex               = vm.Sex;
            entity.AgeMin            = vm.AgeMin;
            entity.AgeMax            = vm.AgeMax;
            entity.SystolicBpWarning  = vm.SystolicBpWarning;
            entity.SystolicBpDanger   = vm.SystolicBpDanger;
            entity.DiastolicBpWarning = vm.DiastolicBpWarning;
            entity.DiastolicBpDanger  = vm.DiastolicBpDanger;
            entity.HeartRateWarningMin = vm.HeartRateWarningMin;
            entity.HeartRateDangerMin  = vm.HeartRateDangerMin;
            entity.HeartRateWarningMax = vm.HeartRateWarningMax;
            entity.HeartRateDangerMax  = vm.HeartRateDangerMax;
            entity.IsActive           = vm.IsActive;
            return entity;
        }

        private static StandardThresholdViewModel MapToViewModel(StandardThreshold e) => new()
        {
            Id               = e.Id,
            Name             = e.Name,
            Description      = e.Description,
            Sex              = e.Sex,
            AgeMin           = e.AgeMin,
            AgeMax           = e.AgeMax,
            SystolicBpWarning  = e.SystolicBpWarning,
            SystolicBpDanger   = e.SystolicBpDanger,
            DiastolicBpWarning = e.DiastolicBpWarning,
            DiastolicBpDanger  = e.DiastolicBpDanger,
            HeartRateWarningMin = e.HeartRateWarningMin,
            HeartRateDangerMin  = e.HeartRateDangerMin,
            HeartRateWarningMax = e.HeartRateWarningMax,
            HeartRateDangerMax  = e.HeartRateDangerMax,
            IsActive           = e.IsActive,
            CreatedAt          = e.CreatedAt,
            UpdatedAt          = e.UpdatedAt,
        };
    }
}
