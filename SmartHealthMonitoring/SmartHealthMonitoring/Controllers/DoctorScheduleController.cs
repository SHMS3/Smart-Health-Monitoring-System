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

        var today = DateTime.Today;
        var endDay = today.AddDays(7);

        var existingSlots = await _context.AppointmentSlots
            .Where(s => s.DoctorId == doctor.Id && s.SlotStart >= today && s.SlotStart < endDay)
            .OrderBy(s => s.SlotStart)
            .ToListAsync();

        // Xóa các slot Available nằm ngoài cửa sổ 7 ngày (ghost slots do worker cũ tạo ra)
        var ghostSlots = await _context.AppointmentSlots
            .Where(s => s.DoctorId == doctor.Id && s.SlotStart >= endDay && s.Status == AppointmentSlotStatus.Available)
            .ToListAsync();
        if (ghostSlots.Any())
        {
            _context.AppointmentSlots.RemoveRange(ghostSlots);
            await _context.SaveChangesAsync();
        }

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

    // POST: /DoctorSchedule/Save
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] List<DoctorSchedule7DaysDto> slots)
    {
        var doctor = await GetCurrentDoctorAsync();
        if (doctor == null) return Forbid();

        var today = DateTime.Today;
        var endDay = today.AddDays(7);

        // 1. Validate overlaps and logical errors within submitted blocks
        var groupedSlots = slots.GroupBy(s => s.Date.Date);
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
                        return BadRequest(new { success = false, message = $"Phát hiện trùng lặp thời gian làm việc vào ngày {dailySlots[i].Date:dd/MM}." });
                    }
                }
            }
        }

        // 2. Load all existing slots for the 7 days
        var existingSlots = await _context.AppointmentSlots
            .Where(s => s.DoctorId == doctor.Id && s.SlotStart >= today && s.SlotStart < endDay)
            .ToListAsync();

        var nonAvailableSlots = existingSlots.Where(s => s.Status == AppointmentSlotStatus.Booked || s.Status == AppointmentSlotStatus.SoftLocked).ToList();

        // 4. Validate that all Booked/SoftLocked slots are STILL COVERED by the new configuration
        foreach (var bookedSlot in nonAvailableSlots)
        {
            var bookedDate = bookedSlot.SlotStart.Date;
            var bookedStartTime = TimeOnly.FromDateTime(bookedSlot.SlotStart);
            var bookedEndTime = TimeOnly.FromDateTime(bookedSlot.SlotEnd);

            var submittedBlocksForDate = groupedSlots.FirstOrDefault(g => g.Key == bookedDate)?.ToList() ?? new List<DoctorSchedule7DaysDto>();
            
            bool isCovered = false;
            foreach (var block in submittedBlocksForDate)
            {
                var blockStart = TimeOnly.Parse(block.StartTime);
                var blockEnd = TimeOnly.Parse(block.EndTime);
                if (bookedStartTime >= blockStart && bookedEndTime <= blockEnd)
                {
                    isCovered = true;
                    break;
                }
            }

            if (!isCovered)
            {
                return BadRequest(new { 
                    success = false, 
                    message = $"Đã có bệnh nhân đặt lịch lúc {bookedSlot.SlotStart:HH:mm} ngày {bookedSlot.SlotStart:dd/MM}. Bạn không thể xóa hoặc thay đổi khung giờ này!" 
                });
            }
        }

        // 5. Delete ALL existing Available and Blocked slots in these 7 days
        var deletableSlots = existingSlots.Where(s => s.Status == AppointmentSlotStatus.Available || s.Status == AppointmentSlotStatus.Blocked).ToList();
        _context.AppointmentSlots.RemoveRange(deletableSlots);

        // 6. Generate new Available slots
        foreach (var block in slots)
        {
            var date = block.Date.Date;
            var current = TimeOnly.Parse(block.StartTime);
            var end = TimeOnly.Parse(block.EndTime);
            var duration = TimeSpan.FromMinutes(block.SlotDurationMinutes);

            while (current.Add(duration) <= end)
            {
                var slotStart = date.Add(current.ToTimeSpan());
                var slotEnd = slotStart.Add(duration);

                // Prevent creating an Available slot if there is already a Booked/SoftLocked slot at this exact time
                bool exists = nonAvailableSlots.Any(s => s.SlotStart == slotStart);
                if (!exists)
                {
                    _context.AppointmentSlots.Add(new AppointmentSlot
                    {
                        DoctorId = doctor.Id,
                        SlotStart = slotStart,
                        SlotEnd = slotEnd,
                        Status = AppointmentSlotStatus.Available
                    });
                }
                current = current.Add(duration);
            }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Lịch làm việc 7 ngày tới đã được cập nhật!";
        return Ok(new { success = true });
    }
}

public class DoctorSchedule7DaysDto
{
    public DateTime Date { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public int SlotDurationMinutes { get; set; } = 30;
}

public class DoctorSchedule7DaysViewModel
{
    public DateTime Date { get; set; }
    public List<DoctorSchedule7DaysDto> Blocks { get; set; } = new();
}
