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
    private readonly IEmailService _emailService;

    public AppointmentController(
        SmartHealthMonitoringContext context,
        IAppointmentService appointmentService,
        IEmailService emailService)
    {
        _context = context;
        _appointmentService = appointmentService;
        _emailService = emailService;
    }

    private async Task<(Patient? patient, Doctor? doctor)> GetCurrentUserAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = User.FindFirstValue(ClaimTypes.Role);

        Patient? patient = role == "0" ? await _context.Patients
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
    [Authorize(Roles = "0")]
    public async Task<IActionResult> FindDoctor(string? specialty, DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.Doctors
            .Include(d => d.User)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(d => d.Specialty.Contains(specialty));

        var doctors = await query.ToListAsync();

        // Với mỗi bác sĩ, lấy số slot còn trống trong 7 ngày tới (từ selectedDate)
        var doctorSlotsData = new List<DoctorSlotViewModel>();
        var endDate = selectedDate.AddDays(6);

        foreach (var doc in doctors)
        {
            var slots = await _appointmentService.GetAvailableSlotsRangeAsync(doc.Id, selectedDate, endDate);
            
            // Nhóm slot theo từng ngày
            var weeklySlots = slots.GroupBy(s => DateOnly.FromDateTime(s.SlotStart.Date))
                                   .ToDictionary(g => g.Key, g => g.ToList());

            doctorSlotsData.Add(new DoctorSlotViewModel
            {
                Doctor       = doc,
                WeeklySlots  = weeklySlots,
                SelectedDate = selectedDate
            });
        }

        // Sắp xếp: Ai có slot trống đưa lên trên cùng, sau đó xếp theo tên
        doctorSlotsData = doctorSlotsData
            .OrderByDescending(d => d.TotalAvailableSlots > 0)
            .ThenByDescending(d => d.TotalAvailableSlots)
            .ThenBy(d => d.Doctor.User.FullName)
            .ToList();

        ViewBag.Specialty    = specialty;
        ViewBag.SelectedDate = selectedDate;
        ViewBag.Specialties  = await _context.Doctors.Select(d => d.Specialty).Distinct().ToListAsync();

        return View(doctorSlotsData);
    }

    // GET: /Appointment/VerifyOtp?slotId=5
    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> VerifyOtp(int slotId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var slot = await _context.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot == null) return NotFound();

        // Cố gắng giữ chỗ (SoftLock) khung giờ cho bệnh nhân hiện tại
        var (lockSuccess, lockMessage) = await _appointmentService.SoftLockSlotAsync(slotId, patient.Id);
        if (!lockSuccess)
        {
            TempData["Error"] = lockMessage;
            return RedirectToAction(nameof(FindDoctor));
        }

        // Sinh OTP ngẫu nhiên 6 chữ số
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString($"BookingOtp_{slotId}", otp);
        HttpContext.Session.SetString($"BookingOtpTime_{slotId}", DateTime.UtcNow.ToString("o"));

        // Gửi email cho bệnh nhân
        var subject = "Mã OTP xác thực đặt lịch khám bệnh - SmartHealth";
        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 8px; max-width: 600px;'>
                <h2 style='color: #27ae60;'>Xác thực đặt lịch khám</h2>
                <p>Kính chào quý khách <strong>{patient.User.FullName}</strong>,</p>
                <p>Bạn đang tiến hành đặt lịch khám chuyên khoa <strong>{slot.Doctor.Specialty}</strong> với bác sĩ <strong>{slot.Doctor.User.FullName}</strong>.</p>
                <p>Mã OTP xác thực của bạn là:</p>
                <div style='background-color: #f7f9fa; padding: 15px; border-radius: 6px; text-align: center; font-size: 24px; font-weight: bold; color: #27ae60; letter-spacing: 5px; margin: 20px 0;'>
                    {otp}
                </div>
                <p style='color: #888; font-size: 12px;'>Mã OTP này có hiệu lực trong 10 phút. Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #aaa;'>Hệ thống Y tế SmartHealth - Đồng hành cùng sức khỏe của bạn.</p>
            </div>";

        try
        {
            await _emailService.SendEmailAsync(patient.User.Email, subject, emailBody);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Không thể gửi email OTP. Vui lòng kiểm tra lại cấu hình mail.";
        }

        ViewBag.SlotId = slotId;
        ViewBag.DoctorName = slot.Doctor.User.FullName;
        ViewBag.Specialty = slot.Doctor.Specialty;
        ViewBag.SlotStart = slot.SlotStart;
        ViewBag.SlotEnd = slot.SlotEnd;

        return View();
    }

    // POST: /Appointment/VerifyOtp
    [HttpPost]
    [Authorize(Roles = "0")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(int slotId, string otpCode)
    {
        var storedOtp = HttpContext.Session.GetString($"BookingOtp_{slotId}");
        var storedOtpTimeStr = HttpContext.Session.GetString($"BookingOtpTime_{slotId}");

        var (patient, _) = await GetCurrentUserAsync();
        var slot = await _context.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null) return NotFound();

        ViewBag.SlotId = slotId;
        ViewBag.DoctorName = slot.Doctor.User.FullName;
        ViewBag.Specialty = slot.Doctor.Specialty;
        ViewBag.SlotStart = slot.SlotStart;
        ViewBag.SlotEnd = slot.SlotEnd;

        if (string.IsNullOrEmpty(storedOtp) || string.IsNullOrEmpty(storedOtpTimeStr) || storedOtp != otpCode.Trim())
        {
            ModelState.AddModelError("", "Mã OTP không chính xác.");
            return View();
        }

        if (DateTime.TryParse(storedOtpTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var otpTime))
        {
            if (DateTime.UtcNow - otpTime > TimeSpan.FromMinutes(10))
            {
                ModelState.AddModelError("", "Mã OTP đã quá hạn 10 phút. Vui lòng quay lại trang chọn bác sĩ để nhận mã mới.");
                return View();
            }
        }

        HttpContext.Session.SetString($"BookingOtpVerified_{slotId}", "true");
        return RedirectToAction(nameof(Book), new { slotId = slotId });
    }

    // GET: /Appointment/Book?slotId=5
    [Authorize(Roles = "0")]
    public async Task<IActionResult> Book(int slotId)
    {
        var verified = HttpContext.Session.GetString($"BookingOtpVerified_{slotId}");
        if (verified != "true")
        {
            TempData["Error"] = "Vui lòng xác thực mã OTP trước khi đặt lịch.";
            return RedirectToAction(nameof(FindDoctor));
        }

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

        var vm = new BookAppointmentViewModel
        {
            SlotId      = slot.Id,
            DoctorName  = slot.Doctor.User.FullName,
            Specialty   = slot.Doctor.Specialty,
            SlotStart   = slot.SlotStart,
            SlotEnd     = slot.SlotEnd,
            PatientNote = ""
        };

        return View(vm);
    }

    // POST: /Appointment/ConfirmBook
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> ConfirmBook(int slotId, string? patientNote)
    {
        var verified = HttpContext.Session.GetString($"BookingOtpVerified_{slotId}");
        if (verified != "true")
        {
            TempData["Error"] = "Vui lòng xác thực mã OTP trước khi đặt lịch.";
            return RedirectToAction(nameof(FindDoctor));
        }

        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var (success, message, appointment) = await _appointmentService.CreatePendingAppointmentAsync(slotId, patient.Id, patientNote);

        if (!success || appointment == null)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(FindDoctor));
        }

        HttpContext.Session.Remove($"BookingOtp_{slotId}");
        HttpContext.Session.Remove($"BookingOtpTime_{slotId}");
        HttpContext.Session.Remove($"BookingOtpVerified_{slotId}");

        // Gửi email thông báo cho bệnh nhân (Mẫu ảnh 2)
        var slot = await _context.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == slotId);

        var subject = $"Thông tin yêu cầu đặt lịch khám - SmartHealth";
        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #e1e8ed; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #27ae60; color: white; padding: 24px; text-align: center;'>
                    <h2 style='margin: 0; font-size: 20px;'>YÊU CẦU ĐẶT LỊCH HẸN KHÁM</h2>
                </div>
                <div style='padding: 24px;'>
                    <p><strong>Kính gửi Quý khách hàng {patient.User.FullName},</strong></p>
                    <p>Hệ thống Y tế SmartHealth đã nhận được yêu cầu về lịch hẹn khám của quý khách. Tổng đài viên của chúng tôi sẽ liên hệ lại qua số điện thoại của quý khách để xác nhận thông tin.</p>
                    <p>Quý khách vui lòng để ý điện thoại và chờ nhân viên liên hệ xác nhận chính xác lịch hẹn khám.</p>
                    
                    <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin: 20px 0;'>
                        <h3 style='margin-top: 0; color: #1e293b; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 8px;'>Chi tiết yêu cầu lịch khám</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 14px;'>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b; width: 140px;'>Bác sĩ:</td>
                                <td style='padding: 6px 0; font-weight: bold;'>BS. {slot.Doctor.User.FullName}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Chuyên khoa:</td>
                                <td>{slot.Doctor.Specialty}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Thời gian khám:</td>
                                <td style='font-weight: bold; color: #27ae60;'>{slot.SlotStart:HH:mm} - {slot.SlotEnd:HH:mm} (Ngày {slot.SlotStart:dd/MM/yyyy})</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Phòng khám:</td>
                                <td>Hệ thống Y tế SmartHealth</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Lý do khám:</td>
                                <td>{patientNote ?? "Không có"}</td>
                            </tr>
                        </table>
                    </div>
                    
                    <p>Cảm ơn quý khách đã lựa chọn dịch vụ của SmartHealth!</p>
                </div>
                <div style='background-color: #f1f5f9; padding: 16px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;'>
                    <p style='margin: 0;'>Đây là email tự động, vui lòng không phản hồi email này.</p>
                </div>
            </div>";

        try
        {
            await _emailService.SendEmailAsync(patient.User.Email, subject, emailBody);
        }
        catch (Exception)
        {
            // Email warning ignored
        }

        return RedirectToAction(nameof(BookingConfirmation), new { appointmentId = appointment.Id });
    }

    // GET: /Appointment/BookingConfirmation
    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> BookingConfirmation(int appointmentId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id);

        if (appointment == null) return NotFound();

        return View(appointment);
    }

    // GET: /Appointment/SupportPortal
    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> SupportPortal(int appointmentId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id);

        if (appointment == null) return NotFound();

        return View(appointment);
    }

    // POST: /Appointment/SupportPortal
    [HttpPost]
    [Authorize(Roles = "0")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SupportPortal(int appointmentId, string reason)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var success = await _appointmentService.RequestCancelAppointmentAsync(appointmentId, reason);
        if (!success)
        {
            TempData["Error"] = "Không thể gửi yêu cầu hủy cho lịch hẹn này.";
            return RedirectToAction(nameof(MyAppointments));
        }

        TempData["Success"] = "Yêu cầu hủy lịch đã được gửi thành công, nhân viên sẽ liên hệ xác nhận sớm.";
        return RedirectToAction(nameof(MyAppointments));
    }

    // GET: /Appointment/MyAppointments
    [Authorize(Roles = "0")]
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
    public Dictionary<DateOnly, List<AppointmentSlot>> WeeklySlots { get; set; } = new();
    public DateOnly SelectedDate { get; set; }
    public int TotalAvailableSlots => WeeklySlots.Values.Sum(v => v.Count);
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
