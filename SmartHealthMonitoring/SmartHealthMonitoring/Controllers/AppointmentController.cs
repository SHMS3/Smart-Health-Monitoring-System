using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using System.Security.Claims;

namespace SmartHealthMonitoring.Controllers;

/// <summary>
/// Luồng đặt lịch khám bệnh.
/// Bệnh nhân (Role=2): Tìm bác sĩ, đặt lịch, xem lịch hẹn.
/// Bác sĩ (Role=1): Xem hàng đợi, hoàn tất khám.
/// </summary>
[Authorize]
public class AppointmentController : Controller
{
    private readonly SmartHealthMonitoringContext _context;
    private readonly IAppointmentService _appointmentService;

    public AppointmentController(SmartHealthMonitoringContext context, IAppointmentService appointmentService)
    {
        _context = context;
        _appointmentService = appointmentService;
    }

    private async Task<(Patient? patient, Doctor? doctor)> GetCurrentUserAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = User.FindFirstValue(ClaimTypes.Role);

        Patient? patient = role == "2" ? await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted) : null;

        Doctor? doctor = role == "1" ? await _context.Doctors
            .FirstOrDefaultAsync(d => d.UserId == userId && !d.IsDeleted) : null;

        return (patient, doctor);
    }

    // ═══════════════════════════════════════════════════════════════
    // BỆNH NHÂN - Tìm bác sĩ & xem lịch trống
    // ═══════════════════════════════════════════════════════════════

    // GET: /Appointment/FindDoctor?specialty=Tim mạch&date=2024-07-01
    [Authorize(Roles = "2")]
    public async Task<IActionResult> FindDoctor(string? specialty, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.Doctors
            .Include(d => d.User)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(d => d.Specialty.Contains(specialty));

        var doctors = await query.ToListAsync();

        // Với mỗi bác sĩ, lấy số slot còn trống
        var doctorSlotsData = new List<DoctorSlotViewModel>();
        foreach (var doc in doctors)
        {
            var slots = await _appointmentService.GetAvailableSlotsAsync(doc.Id, selectedDate);
            doctorSlotsData.Add(new DoctorSlotViewModel
            {
                Doctor         = doc,
                AvailableSlots = slots,
                SelectedDate   = selectedDate
            });
        }

        ViewBag.Specialty    = specialty;
        ViewBag.SelectedDate = selectedDate;
        ViewBag.Specialties  = await _context.Doctors.Select(d => d.Specialty).Distinct().ToListAsync();

        return View(doctorSlotsData);
    }

    // GET: /Appointment/Book?slotId=5
    [Authorize(Roles = "2")]
    public async Task<IActionResult> Book(int slotId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var slot = await _context.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null) return NotFound();

        if (slot.Status == AppointmentSlotStatus.Booked)
        {
            TempData["Error"] = "Khung giờ này đã có người đặt. Vui lòng chọn giờ khác!";
            return RedirectToAction(nameof(FindDoctor));
        }

        // Soft-lock để giữ chỗ 5 phút
        var (softLockSuccess, softLockMsg) = await _appointmentService.SoftLockSlotAsync(slotId, patient.Id);
        if (!softLockSuccess)
        {
            TempData["Error"] = softLockMsg;
            return RedirectToAction(nameof(FindDoctor));
        }

        var vm = new BookAppointmentViewModel
        {
            SlotId      = slot.Id,
            DoctorName  = slot.Doctor.User.FullName,
            Specialty   = slot.Doctor.Specialty,
            SlotStart   = slot.SlotStart,
            SlotEnd     = slot.SlotEnd,
            SoftLockedUntil = DateTime.UtcNow.AddMinutes(5)
        };

        return View(vm);
    }

    // POST: /Appointment/ConfirmBook
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "2")]
    public async Task<IActionResult> ConfirmBook(int slotId, string? patientNote)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var (success, message, appointment) = await _appointmentService.BookSlotAsync(slotId, patient.Id, patientNote);

        if (!success)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(FindDoctor));
        }

        TempData["Success"] = $"🎉 {message}";
        return RedirectToAction(nameof(MyAppointments));
    }

    // GET: /Appointment/MyAppointments
    [Authorize(Roles = "2")]
    public async Task<IActionResult> MyAppointments()
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointments = await _appointmentService.GetPatientAppointmentsAsync(patient.Id);
        return View(appointments);
    }

    // POST: /Appointment/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int appointmentId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = User.FindFirstValue(ClaimTypes.Role);
        bool isDoctor = role == "1";

        var (success, message) = await _appointmentService.CancelAppointmentAsync(appointmentId, userId, isDoctor);

        TempData[success ? "Success" : "Error"] = message;

        return isDoctor
            ? RedirectToAction(nameof(DoctorQueue))
            : RedirectToAction(nameof(MyAppointments));
    }

    // ═══════════════════════════════════════════════════════════════
    // BÁC SĨ - Hàng đợi bệnh nhân
    // ═══════════════════════════════════════════════════════════════

    // GET: /Appointment/DoctorQueue?date=2024-07-01
    [Authorize(Roles = "1")]
    public async Task<IActionResult> DoctorQueue(DateOnly? date)
    {
        var (_, doctor) = await GetCurrentUserAsync();
        if (doctor == null) return Forbid();

        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var queue = await _appointmentService.GetDoctorQueueAsync(doctor.Id, selectedDate);

        ViewBag.SelectedDate = selectedDate;
        ViewBag.DoctorId     = doctor.Id;
        return View(queue);
    }

    // POST: /Appointment/Complete — Liên kết hồ sơ bệnh án
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "1")]
    public async Task<IActionResult> Complete(int appointmentId, int clinicalRecordId)
    {
        await _appointmentService.CompleteAppointmentAsync(appointmentId, clinicalRecordId);
        TempData["Success"] = "Đã hoàn tất lịch hẹn và liên kết hồ sơ bệnh án.";
        return RedirectToAction(nameof(DoctorQueue));
    }

    // GET: /Appointment/GetSlots?doctorId=1&date=2024-07-01 (AJAX)
    [HttpGet]
    public async Task<IActionResult> GetSlots(int doctorId, DateOnly date)
    {
        var slots = await _appointmentService.GetAvailableSlotsAsync(doctorId, date);
        return Json(slots.Select(s => new
        {
            s.Id,
            Start    = s.SlotStart.ToString("HH:mm"),
            End      = s.SlotEnd.ToString("HH:mm"),
            s.Status
        }));
    }
}

// ─── ViewModels ───────────────────────────────────────────────────
public class DoctorSlotViewModel
{
    public Doctor Doctor { get; set; } = null!;
    public List<AppointmentSlot> AvailableSlots { get; set; } = new();
    public DateOnly SelectedDate { get; set; }
}

public class BookAppointmentViewModel
{
    public int SlotId { get; set; }
    public string DoctorName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public string? PatientNote { get; set; }
    public DateTime SoftLockedUntil { get; set; }
}
