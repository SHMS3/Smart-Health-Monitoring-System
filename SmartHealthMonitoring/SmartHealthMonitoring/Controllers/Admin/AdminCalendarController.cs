using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Admin;
using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Controllers.Admin;

[Authorize(Roles = "2")]
public class AdminCalendarController : Controller
{
    private readonly IAdminCalendarService _calendarService;

    public AdminCalendarController(IAdminCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? weekStart)
    {
        var vm = await _calendarService.GetWeekSummaryAsync(weekStart);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Events(DateTime start, DateTime end, int? doctorId)
    {
        var events = await _calendarService.GetCalendarEventsAsync(start, end, doctorId);
        return Json(events);
    }

    [HttpGet]
    public async Task<IActionResult> NoShowReport(DateOnly? from, DateOnly? to)
    {
        var vm = await _calendarService.GetNoShowReportAsync(from, to);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Heatmap(DateOnly? from, DateOnly? to, int slotMinutes = 60)
    {
        var vm = await _calendarService.GetHeatmapAsync(from, to, slotMinutes);
        return View(vm);
    }
}
