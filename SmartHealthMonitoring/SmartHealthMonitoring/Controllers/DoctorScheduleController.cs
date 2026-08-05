using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHealthMonitoring.Interfaces.Doctor;
using SmartHealthMonitoring.ViewModels.Doctor;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers;

[Authorize(Roles = "1")]
public class DoctorScheduleController : Controller
{
    private readonly IDoctorScheduleService _scheduleService;

    public DoctorScheduleController(IDoctorScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var doctor = await _scheduleService.GetDoctorByUserIdAsync(userId);
        if (doctor == null) return Forbid();

        await _scheduleService.CleanupGhostSlotsAsync(doctor.Id);

        var existingSlots = await _scheduleService.GetWeekSlotsAsync(doctor.Id);
        
        var today = SmartHealthMonitoring.Common.AppTime.Now.Date;
        var viewModels = new List<DoctorSchedule7DaysViewModel>();
        
        for (int i = 0; i < 7; i++)
        {
            var date = today.AddDays(i);
            var dailySlots = existingSlots.Where(s => s.SlotStart.Date == date).OrderBy(s => s.SlotStart).ToList();
            
            var blocks = new List<DoctorSchedule7DaysDto>();
            if (dailySlots.Any())
            {
                DateTime currentBlockStart = dailySlots.First().SlotStart;
                DateTime currentBlockEnd = dailySlots.First().SlotEnd;
                
                for (int j = 1; j < dailySlots.Count; j++)
                {
                    if (dailySlots[j].SlotStart == currentBlockEnd)
                    {
                        currentBlockEnd = dailySlots[j].SlotEnd;
                    }
                    else
                    {
                        blocks.Add(new DoctorSchedule7DaysDto
                        {
                            Date = date,
                            StartTime = currentBlockStart.ToString("HH:mm"),
                            EndTime = currentBlockEnd.ToString("HH:mm")
                        });
                        currentBlockStart = dailySlots[j].SlotStart;
                        currentBlockEnd = dailySlots[j].SlotEnd;
                    }
                }
                blocks.Add(new DoctorSchedule7DaysDto
                {
                    Date = date,
                    StartTime = currentBlockStart.ToString("HH:mm"),
                    EndTime = currentBlockEnd.ToString("HH:mm")
                });
            }

            viewModels.Add(new DoctorSchedule7DaysViewModel
            {
                Date = date,
                Blocks = blocks
            });
        }

        ViewBag.DoctorId = doctor.Id;
        return View(viewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] List<DoctorSchedule7DaysDto> slots)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var doctor = await _scheduleService.GetDoctorByUserIdAsync(userId);
        if (doctor == null) return Forbid();

        var (success, error) = await _scheduleService.SaveScheduleAsync(doctor.Id, slots);

        if (!success)
        {
            return BadRequest(new { success = false, message = error });
        }

        TempData["Success"] = "L?ch l�m vi?c 7 ng�y t?i d� du?c c?p nh?t!";
        return Ok(new { success = true });
    }
}
