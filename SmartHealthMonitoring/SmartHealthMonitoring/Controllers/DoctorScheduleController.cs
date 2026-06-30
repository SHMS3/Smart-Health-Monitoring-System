using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers;

/// <summary>
/// Quản lý lịch làm việc hàng tuần của bác sĩ.
/// Chỉ Role=1 (Bác sĩ) mới được truy cập.
/// </summary>
[Authorize(Roles = "1")]
public class DoctorScheduleController : Controller
{
    private readonly SmartHealthMonitoringContext _context;

    public DoctorScheduleController(SmartHealthMonitoringContext context)
    {
        _context = context;
    }

    private async Task<Doctor?> GetCurrentDoctorAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted);
    }

    // GET: /DoctorSchedule
    public async Task<IActionResult> Index()
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return Forbid();

        var schedules = await _context.DoctorWorkSchedules
            .Where(s => s.DoctorId == doctor.Id)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync();

        ViewBag.DoctorId = doctor.Id;
        return View(schedules);
    }

    // POST: /DoctorSchedule/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] List<DoctorWorkScheduleDto> slots)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return Forbid();

        // Xóa tất cả schedule cũ và tạo lại
        var existing = _context.DoctorWorkSchedules.Where(s => s.DoctorId == doctor.Id);
        _context.DoctorWorkSchedules.RemoveRange(existing);

        foreach (var slot in slots)
        {
            _context.DoctorWorkSchedules.Add(new DoctorWorkSchedule
            {
                DoctorId           = doctor.Id,
                DayOfWeek          = slot.DayOfWeek,
                StartTime          = TimeOnly.Parse(slot.StartTime),
                EndTime            = TimeOnly.Parse(slot.EndTime),
                SlotDurationMinutes = slot.SlotDurationMinutes,
                IsActive           = true
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Lịch làm việc đã được cập nhật!";
        return Ok(new { success = true });
    }

    // POST: /DoctorSchedule/BlockTime
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockTime(DateTime blockStart, DateTime blockEnd)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return Forbid();

        var slotsToBlock = await _context.AppointmentSlots
            .Where(s =>
                s.DoctorId == doctor.Id &&
                s.SlotStart >= blockStart &&
                s.SlotStart < blockEnd &&
                s.Status == AppointmentSlotStatus.Available)
            .ToListAsync();

        foreach (var slot in slotsToBlock)
            slot.Status = AppointmentSlotStatus.Blocked;

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã block {slotsToBlock.Count} khung giờ.";
        return RedirectToAction(nameof(Index));
    }
}

public class DoctorWorkScheduleDto
{
    public byte DayOfWeek { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public int SlotDurationMinutes { get; set; } = 30;
}
