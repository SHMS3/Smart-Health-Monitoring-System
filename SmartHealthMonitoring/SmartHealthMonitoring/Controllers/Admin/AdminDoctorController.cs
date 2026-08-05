using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin
{
    [Authorize(Roles = "2")]
    public class AdminDoctorController : Controller
    {
        private readonly IAdminDoctorService _doctorService;

        public AdminDoctorController(IAdminDoctorService doctorService)
        {
            _doctorService = doctorService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var result = await _doctorService.GetDoctorsPagedAsync(page, pageSize);
            return View(result);
        }

        [HttpGet]
        public IActionResult Create() => View(new DoctorCreateViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DoctorCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                string loginUrl = Url.Action("Login", "Auth", new { returnUrl = "/Home/Profile?tab=security" }, Request.Scheme) ?? "";
                await _doctorService.CreateDoctorAsync(model, loginUrl);
                
                TempData["Success"] = "�� c?p t�i kho?n B�c si th�nh c�ng v� g?i email m?t kh?u m?c d?nh.";
                return RedirectToAction(nameof(Index));
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("Email", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "L?i h? th?ng: " + ex.Message;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _doctorService.GetDoctorForEditAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DoctorEditViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                await _doctorService.UpdateDoctorAsync(model);
                TempData["Success"] = "C?p nh?t th�ng tin b�c si th�nh c�ng.";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError("Email", ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "L?i h? th?ng: " + ex.Message;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(int userId, string? lockReason)
        {
            try
            {
                await _doctorService.ToggleLockAsync(userId, lockReason);
                TempData["Success"] = "�� thay d?i tr?ng th�i kh�a t�i kho?n b�c si.";
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Kh�ng t�m th?y ngu?i d�ng.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}


