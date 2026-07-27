using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "3")] // Assuming 3 is Receptionist
    public class NewsController : Controller
    {
        private readonly INewsService _newsService;

        public NewsController(INewsService newsService)
        {
            _newsService = newsService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status)
        {
            var news = await _newsService.GetNewsAsync(status);
            
            ViewBag.CurrentStatus = status ?? "all";
            ViewBag.CurrentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            
            return View(news);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new HealthNewsPost
            {
                Status = "Draft"
            };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HealthNewsPost model, string action, IFormFile? uploadImage)
        {
            ModelState.Remove("AuthorName");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("Status");
            ModelState.Remove("RejectionReason");

            if (!ModelState.IsValid)
                return View(model);

            var authorName = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            
            var (success, message) = await _newsService.CreateNewsAsync(model, action, uploadImage, authorName);

            if (!success)
            {
                ModelState.AddModelError("", message);
                return View(model);
            }

            if (success)
            {
                TempData["Success"] = message;
            }
            else
            {
                TempData["Error"] = message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var currentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            var (success, message, post) = await _newsService.GetNewsForEditAsync(id, currentAuthor);

            if (!success)
            {
                if (message == "Không tìm thấy bài viết.") return NotFound();
                
                TempData["Error"] = message;
                return RedirectToAction(nameof(Index));
            }
            
            return View(post);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HealthNewsPost model, string action, IFormFile? uploadImage)
        {
            if (id != model.Id) return BadRequest();

            ModelState.Remove("AuthorName");
            ModelState.Remove("CreatedAt");
            ModelState.Remove("Status");
            ModelState.Remove("RejectionReason");

            if (!ModelState.IsValid)
                return View(model);

            var currentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            
            var (success, message) = await _newsService.UpdateNewsAsync(id, model, action, uploadImage, currentAuthor);

            if (!success)
            {
                if (message == "Không tìm thấy bài viết.") return NotFound();
                
                ModelState.AddModelError("", message);
                return View(model);
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var currentAuthor = User.FindFirstValue(ClaimTypes.Name) ?? "Lễ tân";
            var (success, message) = await _newsService.DeleteNewsAsync(id, currentAuthor);

            return Json(new { success, message });
        }
    }
}
