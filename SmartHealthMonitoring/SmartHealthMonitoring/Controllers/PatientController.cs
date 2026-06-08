using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Services;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Controllers
{
    [Authorize(Roles = "0")]
    public class PatientController : Controller
    {
        private readonly DailyVitalLogService _dailyVitalLogService;
        public PatientController(DailyVitalLogService dailyVitalLogService)
        {
            _dailyVitalLogService = dailyVitalLogService;
        }

        [HttpGet("history")]
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            int patientId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var todayLogs = (await _dailyVitalLogService
                .GetLogsByDateAsync(patientId, DateTime.Today))
                .OrderByDescending(x => x.LoggedAt)
                .ToList();

            var lastLog = todayLogs.FirstOrDefault();

            bool canLog = true;
            string logMessage = "Ghi log hôm nay";

            DateTime? nextLogTime = null;
            int remainingSeconds = 0;

            if (todayLogs.Count >= 10)
            {
                canLog = false;
                logMessage = "Đã đạt giới hạn 10 lần/ngày";
            }
            else if (lastLog != null)
            {
               nextLogTime = lastLog.LoggedAt.AddHours(1);
                //nextLogTime = lastLog.LoggedAt.AddSeconds(10);
                if (DateTime.Now < nextLogTime)
                {
                    canLog = false;
                    logMessage = "Đang trong thời gian chờ";

                    remainingSeconds = (int)(nextLogTime.Value - DateTime.Now).TotalSeconds;
                }
            }

            ViewBag.CanLog = canLog;
            ViewBag.LogMessage = logMessage;
            ViewBag.NextLogTime = nextLogTime;
            ViewBag.RemainingSeconds = remainingSeconds;

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            try
            {
                PagedResult<DailyVitalLogViewModel> result;

                if (!fromDate.HasValue && !toDate.HasValue)
                {
                    result = await _dailyVitalLogService.GetPatientVitalsHistoryAsync(
                        patientId,
                        DateTime.Today,
                        DateTime.Today,
                        page,
                        5);
                }
                else
                {
                    result = await _dailyVitalLogService.GetPatientVitalsHistoryAsync(
                        patientId,
                        fromDate,
                        toDate,
                        page,
                        5);
                }

                return View(result);
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return View(new PagedResult<DailyVitalLogViewModel>());
            }
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DailyVitalLogViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            int patientId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            try
            {
                await _dailyVitalLogService.CreateLogAsync(patientId, model);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _dailyVitalLogService.GetDailyLogDetailsAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ chỉ số này hoặc hồ sơ đã bị xóa.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }


        [HttpGet("update/{id}")]
        public async Task<IActionResult> Update(int id)
        {
            var model = await _dailyVitalLogService.GetLogForUpdateAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hồ sơ.";
                return RedirectToAction(nameof(Index));
            }

            if (model.UpdateCount >= 2)
            {
                TempData["ErrorMessage"] = "Hồ sơ đã bị khóa.";

                return RedirectToAction(nameof(Details), new { id = model.Id });
            }

            return View(model);
        }

        [HttpPost("update/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, DailyVitalLogViewModel model)
        {
            if (id != model.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var success = await _dailyVitalLogService.UpdateLogAsync(id, model);
                    if (success)
                    {
                        TempData["SuccessMessage"] = "Cập nhật chỉ số thành công!";
                        return RedirectToAction(nameof(Details), new { id = model.Id });
                    }
                }
                catch (InvalidOperationException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                    return RedirectToAction(nameof(Details), new { id = model.Id });
                }
            }

            return View(model);
        }
    }
}