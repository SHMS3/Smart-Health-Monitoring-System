using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
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
    private readonly IAppointmentService _appointmentService;

    public DoctorScheduleController(SmartHealthMonitoringContext context, IAppointmentService appointmentService)
    {
        _context = context;
        _appointmentService = appointmentService;
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

        // 1. Validate overlaps and logical errors
        var groupedSlots = slots.GroupBy(s => s.DayOfWeek);
        foreach (var group in groupedSlots)
        {
            var dailySlots = group.OrderBy(s => TimeOnly.Parse(s.StartTime)).ToList();
            for (int i = 0; i < dailySlots.Count; i++)
            {
                var currentStart = TimeOnly.Parse(dailySlots[i].StartTime);
                var currentEnd = TimeOnly.Parse(dailySlots[i].EndTime);

                if (currentStart >= currentEnd)
                {
                    return BadRequest(new { success = false, message = "Giờ bắt đầu phải nhỏ hơn giờ kết thúc." });
                }

                if (i > 0)
                {
                    var prevEnd = TimeOnly.Parse(dailySlots[i - 1].EndTime);
                    if (currentStart < prevEnd)
                    {
                        return BadRequest(new { success = false, message = $"Phát hiện trùng lặp thời gian làm việc vào Thứ {dailySlots[i].DayOfWeek}." });
                    }
                }
            }
        }

        // 2. Xóa tất cả schedule cũ và tạo lại
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
        
        // 3. Tự động cập nhật lại các Slot cho 14 ngày tiếp theo ngay lập tức
        await _appointmentService.RefreshDoctorSlotsAsync(doctor.Id);

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
