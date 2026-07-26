using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    // GET: /Appointment/FindDoctor
    [Authorize(Roles = "0")]
    public async Task<IActionResult> FindDoctor(string? specialty, string? doctorName, DateOnly? fromDate, DateOnly? toDate, byte? gender, string? session, string? roomNumber)
    {
        var (patient, _) = await GetCurrentUserAsync();
        var patientId = patient?.Id;

        var startDate = fromDate ?? DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
        var endDate = toDate ?? startDate.AddDays(6);
        if (endDate < startDate) endDate = startDate;

        var query = _context.Doctors
            .Include(d => d.User)
            .Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(d => d.Specialty.Contains(specialty));

        if (!string.IsNullOrWhiteSpace(doctorName))
            query = query.Where(d => d.User.FullName.Contains(doctorName));

        if (gender.HasValue)
            query = query.Where(d => d.Sex == gender.Value);

        if (!string.IsNullOrWhiteSpace(roomNumber))
            query = query.Where(d => d.RoomNumber == roomNumber);

        var doctors = await query.ToListAsync();
        var doctorIds = doctors.Select(d => d.Id).ToList();

        var allSlots = await _appointmentService.GetAvailableSlotsRangeForDoctorsAsync(doctorIds, startDate, endDate, patientId);

        if (!string.IsNullOrEmpty(session))
        {
            if (session == "Morning")
                allSlots = allSlots.Where(s => s.SlotStart.Hour < 12).ToList();
            else if (session == "Afternoon")
                allSlots = allSlots.Where(s => s.SlotStart.Hour >= 12).ToList();
        }

        var slotsByDoctor = allSlots.GroupBy(s => s.DoctorId).ToDictionary(g => g.Key, g => g.ToList());

        var doctorSlotsData = new List<DoctorSlotViewModel>();

        foreach (var doc in doctors)
        {
            var slots = slotsByDoctor.GetValueOrDefault(doc.Id, new List<AppointmentSlot>());
            var weeklySlots = slots.GroupBy(s => DateOnly.FromDateTime(s.SlotStart.Date))
                                   .ToDictionary(g => g.Key, g => g.ToList());

            doctorSlotsData.Add(new DoctorSlotViewModel
            {
                Doctor       = doc,
                WeeklySlots  = weeklySlots,
                SelectedDate = startDate
            });
        }

        bool hasFilter = !string.IsNullOrWhiteSpace(specialty) || 
                         !string.IsNullOrWhiteSpace(doctorName) || 
                         gender.HasValue || 
                         !string.IsNullOrWhiteSpace(session) || 
                         !string.IsNullOrWhiteSpace(roomNumber) || 
                         fromDate.HasValue || 
                         toDate.HasValue;

        if (hasFilter)
        {
            doctorSlotsData = doctorSlotsData.Where(d => d.TotalAvailableSlots > 0).ToList();
        }

        doctorSlotsData = doctorSlotsData
            .OrderByDescending(d => d.TotalAvailableSlots > 0)
            .ThenByDescending(d => d.TotalAvailableSlots)
            .ThenBy(d => d.Doctor.User.FullName)
            .ToList();

        ViewBag.Specialty    = specialty;
        ViewBag.DoctorName   = doctorName;
        ViewBag.FromDate     = startDate;
        ViewBag.ToDate       = endDate;
        ViewBag.Gender       = gender;
        ViewBag.Session      = session;
        ViewBag.RoomNumber   = roomNumber;
        
        ViewBag.Specialties  = await _context.Doctors.Select(d => d.Specialty).Distinct().ToListAsync();
        ViewBag.RoomNumbers  = await _context.Doctors.Where(d => d.RoomNumber != null).Select(d => d.RoomNumber).Distinct().ToListAsync();

        return View(doctorSlotsData);
    }

    // GET: /Appointment/VerifyOtp?slotId=5
    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> VerifyOtp(int slotId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        if (string.IsNullOrWhiteSpace(patient.User.FullName) ||
            string.IsNullOrWhiteSpace(patient.Phone) ||
            string.IsNullOrWhiteSpace(patient.CitizenId))
        {
            TempData["Error"] = "Vui lòng cập nhật đầy đủ thông tin cá nhân (Họ tên, Ngày sinh, Giới tính, SĐT, CCCD) trong Hồ sơ cá nhân trước khi đặt lịch.";
            return RedirectToAction("Profile", "Home");
        }

        var slot = await _context.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot == null) return NotFound();

        var hasActiveOrPending = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patient.Id &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
        if (hasActiveOrPending)
        {
            TempData["Error"] = "Bạn đang có lịch khám chưa hoàn thành hoặc yêu cầu đặt lịch đang chờ duyệt. Không thể đặt thêm lịch mới.";
            return RedirectToAction(nameof(FindDoctor));
        }

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
        HttpContext.Session.SetString($"BookingOtpTime_{slotId}", SmartHealthMonitoring.Common.AppTime.Now.ToString("o"));

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

        var toEmail = patient.User.Email;
        var serviceScopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            try
            {
                await emailService.SendEmailAsync(toEmail, subject, emailBody);
            }
            catch { /* Ignore warning in background */ }
        });

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

        var hasActiveOrPending = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patient.Id &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
        if (hasActiveOrPending)
        {
            TempData["Error"] = "Bạn đang có lịch khám chưa hoàn thành hoặc yêu cầu đặt lịch đang chờ duyệt. Không thể đặt thêm lịch mới.";
            return RedirectToAction(nameof(FindDoctor));
        }

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
            if (SmartHealthMonitoring.Common.AppTime.Now - otpTime > TimeSpan.FromMinutes(10))
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

        var hasActiveOrPending = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patient.Id &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
        if (hasActiveOrPending)
        {
            TempData["Error"] = "Bạn đang có lịch khám chưa hoàn thành hoặc yêu cầu đặt lịch đang chờ duyệt. Không thể đặt thêm lịch mới.";
            return RedirectToAction(nameof(FindDoctor));
        }

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

        var hasActiveOrPending = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patient.Id &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
        if (hasActiveOrPending)
        {
            TempData["Error"] = "Bạn đang có lịch khám chưa hoàn thành hoặc yêu cầu đặt lịch đang chờ duyệt. Không thể đặt thêm lịch mới.";
            return RedirectToAction(nameof(FindDoctor));
        }

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
                                <td style='font-weight: bold; color: #92400e;'>{(slot.Doctor.RoomNumber ?? "Chưa phân phòng")} — Hệ thống Y tế SmartHealth</td>
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

        var toEmail = patient.User.Email;
        var serviceScopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            try
            {
                await emailService.SendEmailAsync(toEmail, subject, emailBody);
            }
            catch { /* Ignore warning in background */ }
        });

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

        // SCH-07: Load waitlist items
        ViewBag.WaitlistItems = await _appointmentService.GetPatientWaitlistAsync(patient.Id);

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

        var selectedDate = date ?? DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
        var todayUtc = SmartHealthMonitoring.Common.AppTime.Now.Date;
        var endDate = todayUtc.AddDays(30);

        // Load ALL appointments in next 30 days for the calendar view
        var allAppointments = await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.Slot.DoctorId == doctor.Id
                     && a.Slot.SlotStart >= todayUtc
                     && a.Slot.SlotStart < endDate
                     && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Completed))
            .OrderBy(a => a.Slot.SlotStart)
            .ToListAsync();

        // Group by date
        var byDate = allAppointments
            .GroupBy(a => DateOnly.FromDateTime(a.Slot.SlotStart.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Single-day queue for selected date (for detail panel)
        var queue = allAppointments
            .Where(a => DateOnly.FromDateTime(a.Slot.SlotStart.Date) == selectedDate)
            .Where(a => a.PatientNote != "Đăng ký trực tiếp tại quầy lễ tân")
            .ToList();

        // Walk-in queue (only for today)
        var waitingQueue = new List<WaitingPatient>();
        if (selectedDate == DateOnly.FromDateTime(todayUtc))
        {
            waitingQueue = await _context.WaitingPatients
                .Include(w => w.Patient).ThenInclude(p => p.User)
                .Where(w => w.CreatedAt >= todayUtc
                         && w.DoctorId == doctor.Id
                         && (w.Status == 0 || w.Status == 1))
                .OrderBy(w => w.SequenceNumber)
                .ToListAsync();
        }

        var patientIds = waitingQueue.Select(w => w.PatientId).ToList();
        patientIds.AddRange(queue.Select(a => a.PatientId));
        patientIds = patientIds.Distinct().ToList();
        
        var paidPayments = await _context.Payments
            .Where(p => patientIds.Contains(p.PatientId) && p.CreatedAt.Date == todayUtc && p.Status == "Paid")
            .Select(p => p.PatientId).Distinct().ToListAsync();
        var pendingPayments = await _context.Payments
            .Where(p => patientIds.Contains(p.PatientId) && p.CreatedAt.Date == todayUtc && p.Status == "Pending")
            .Select(p => p.PatientId).Distinct().ToListAsync();
        var onlyPendingPayments = pendingPayments.Except(paidPayments).ToList();
        waitingQueue = waitingQueue
            .OrderBy(w => onlyPendingPayments.Contains(w.PatientId) ? 1 : 0)
            .ThenBy(w => w.SequenceNumber).ToList();

        ViewBag.WaitingQueue     = waitingQueue;
        ViewBag.PaidPayments     = paidPayments;
        ViewBag.PendingPayments  = onlyPendingPayments;
        ViewBag.SelectedDate     = selectedDate;
        ViewBag.DoctorId         = doctor.Id;
        ViewBag.AllByDate        = byDate;
        ViewBag.TodayDate        = DateOnly.FromDateTime(todayUtc);
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
        var (patient, _) = await GetCurrentUserAsync();
        var patientId = patient?.Id;

        var slots = await _appointmentService.GetAvailableSlotsAsync(doctorId, date, patientId);
        return Json(slots.Select(s => new
        {
            s.Id,
            Start    = s.SlotStart.ToString("HH:mm"),
            End      = s.SlotEnd.ToString("HH:mm"),
            s.Status
        }));
    }

    // ═══════════════════════════════════════════════════════════════
    // SCH-05: CANCEL DIRECT (AJAX)
    // ═══════════════════════════════════════════════════════════════

    // POST: /Appointment/CancelDirect (AJAX JSON)
    [HttpPost]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> CancelDirect([FromBody] CancelDirectRequest request)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Json(new { success = false, message = "Unauthorized" });

        var (success, message) = await _appointmentService.CancelDirectAsync(request.AppointmentId, patient.Id);
        return Json(new { success, message });
    }

    // ═══════════════════════════════════════════════════════════════
    // SCH-06: RESCHEDULE
    // ═══════════════════════════════════════════════════════════════

    // GET: /Appointment/Reschedule?appointmentId=5
    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> Reschedule(int appointmentId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id);

        if (appointment == null) return NotFound();

        if (appointment.Status != SmartHealthMonitoring.Models.AppointmentStatus.Confirmed)
        {
            TempData["Error"] = "Chỉ có thể dời lịch hẹn đã xác nhận.";
            return RedirectToAction(nameof(MyAppointments));
        }

        var vm = new RescheduleViewModel
        {
            AppointmentId = appointment.Id,
            DoctorId = appointment.DoctorId,
            DoctorName = appointment.Doctor.User.FullName,
            Specialty = appointment.Doctor.Specialty,
            RoomNumber = appointment.Doctor.RoomNumber,
            CurrentSlotStart = appointment.Slot.SlotStart,
            CurrentSlotEnd = appointment.Slot.SlotEnd
        };

        return View(vm);
    }

    // GET: /Appointment/RescheduleVerifyOtp?appointmentId=5&newSlotId=10
    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> RescheduleVerifyOtp(int appointmentId, int newSlotId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient.Id);

        if (appointment == null) return NotFound();

        var newSlot = await _context.AppointmentSlots.FindAsync(newSlotId);
        if (newSlot == null) return NotFound();

        // SoftLock slot mới
        var (lockSuccess, lockMessage) = await _appointmentService.SoftLockSlotAsync(newSlotId, patient.Id);
        if (!lockSuccess)
        {
            TempData["Error"] = lockMessage;
            return RedirectToAction(nameof(Reschedule), new { appointmentId });
        }

        // Sinh OTP
        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString($"RescheduleOtp_{appointmentId}_{newSlotId}", otp);
        HttpContext.Session.SetString($"RescheduleOtpTime_{appointmentId}_{newSlotId}", SmartHealthMonitoring.Common.AppTime.Now.ToString("o"));

        // Gửi email OTP
        var subject = "Mã OTP xác thực dời lịch khám - SmartHealth";
        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 8px; max-width: 600px;'>
                <h2 style='color: #6366f1;'>Xác thực dời lịch khám</h2>
                <p>Kính chào <strong>{patient.User.FullName}</strong>,</p>
                <p>Bạn đang dời lịch hẹn khám <strong>{appointment.Doctor.Specialty}</strong> với BS. <strong>{appointment.Doctor.User.FullName}</strong>.</p>
                <p>Từ: <strong>{appointment.Slot.SlotStart:HH:mm dd/MM/yyyy}</strong> → Sang: <strong>{newSlot.SlotStart:HH:mm dd/MM/yyyy}</strong></p>
                <p>Mã OTP xác thực:</p>
                <div style='background-color: #f7f9fa; padding: 15px; border-radius: 6px; text-align: center; font-size: 24px; font-weight: bold; color: #6366f1; letter-spacing: 5px; margin: 20px 0;'>
                    {otp}
                </div>
                <p style='color: #888; font-size: 12px;'>Mã OTP có hiệu lực trong 10 phút.</p>
            </div>";

        var toEmail = patient.User.Email;
        var serviceScopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();
            try { await emailSvc.SendEmailAsync(toEmail, subject, emailBody); }
            catch { /* ignore */ }
        });

        ViewBag.AppointmentId = appointmentId;
        ViewBag.NewSlotId = newSlotId;
        ViewBag.DoctorName = appointment.Doctor.User.FullName;
        ViewBag.Specialty = appointment.Doctor.Specialty;
        ViewBag.OldSlot = $"{appointment.Slot.SlotStart:HH:mm} – {appointment.Slot.SlotEnd:HH:mm}, {appointment.Slot.SlotStart:dd/MM/yyyy}";
        ViewBag.NewSlot = $"{newSlot.SlotStart:HH:mm} – {newSlot.SlotEnd:HH:mm}, {newSlot.SlotStart:dd/MM/yyyy}";

        return View();
    }

    // POST: /Appointment/RescheduleVerifyOtp
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> RescheduleVerifyOtp(int appointmentId, int newSlotId, string otpCode)
    {
        var key = $"RescheduleOtp_{appointmentId}_{newSlotId}";
        var timeKey = $"RescheduleOtpTime_{appointmentId}_{newSlotId}";
        var storedOtp = HttpContext.Session.GetString(key);
        var storedOtpTimeStr = HttpContext.Session.GetString(timeKey);

        var (patient, _) = await GetCurrentUserAsync();
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patient!.Id);
        var newSlot = await _context.AppointmentSlots.FindAsync(newSlotId);

        if (appointment == null || newSlot == null) return NotFound();

        ViewBag.AppointmentId = appointmentId;
        ViewBag.NewSlotId = newSlotId;
        ViewBag.DoctorName = appointment.Doctor.User.FullName;
        ViewBag.Specialty = appointment.Doctor.Specialty;
        ViewBag.OldSlot = $"{appointment.Slot.SlotStart:HH:mm} – {appointment.Slot.SlotEnd:HH:mm}, {appointment.Slot.SlotStart:dd/MM/yyyy}";
        ViewBag.NewSlot = $"{newSlot.SlotStart:HH:mm} – {newSlot.SlotEnd:HH:mm}, {newSlot.SlotStart:dd/MM/yyyy}";

        if (string.IsNullOrEmpty(storedOtp) || storedOtp != otpCode?.Trim())
        {
            ModelState.AddModelError("", "Mã OTP không chính xác.");
            return View();
        }

        if (DateTime.TryParse(storedOtpTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var otpTime))
        {
            if (SmartHealthMonitoring.Common.AppTime.Now - otpTime > TimeSpan.FromMinutes(10))
            {
                ModelState.AddModelError("", "Mã OTP đã hết hạn. Vui lòng thử lại.");
                return View();
            }
        }

        // OTP OK → thực hiện reschedule
        var (success, message, newAppt) = await _appointmentService.RescheduleAppointmentAsync(
            appointmentId, newSlotId, patient!.Id);

        HttpContext.Session.Remove(key);
        HttpContext.Session.Remove(timeKey);

        if (!success || newAppt == null)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(MyAppointments));
        }

        // Gửi email xác nhận dời lịch
        var emailSubject = "Thông tin dời lịch hẹn khám - SmartHealth";
        var emailBody = $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e1e8ed;border-radius:8px;overflow:hidden'>
                <div style='background:linear-gradient(135deg,#6366f1,#818cf8);color:white;padding:24px;text-align:center'>
                    <h2 style='margin:0'>DỜI LỊCH HẸN KHÁM THÀNH CÔNG</h2>
                </div>
                <div style='padding:24px'>
                    <p>Kính gửi <strong>{patient.User.FullName}</strong>,</p>
                    <p>Lịch hẹn của bạn đã được dời thành công. Vui lòng chờ nhân viên xác nhận.</p>
                    <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:16px;margin:16px 0'>
                        <p style='margin:4px 0'><strong>Bác sĩ:</strong> BS. {appointment.Doctor.User.FullName}</p>
                        <p style='margin:4px 0'><strong>Lịch cũ:</strong> <del>{appointment.Slot.SlotStart:HH:mm} - {appointment.Slot.SlotEnd:HH:mm}, {appointment.Slot.SlotStart:dd/MM/yyyy}</del></p>
                        <p style='margin:4px 0;color:#16a34a'><strong>Lịch mới:</strong> {newSlot.SlotStart:HH:mm} - {newSlot.SlotEnd:HH:mm}, {newSlot.SlotStart:dd/MM/yyyy}</p>
                    </div>
                </div>
            </div>";

        var toEmail = patient.User.Email;
        var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();
            try { await emailSvc.SendEmailAsync(toEmail, emailSubject, emailBody); }
            catch { /* ignore */ }
        });

        TempData["Success"] = message;
        return RedirectToAction(nameof(MyAppointments));
    }

    // ═══════════════════════════════════════════════════════════════
    // SCH-07: WAITLIST
    // ═══════════════════════════════════════════════════════════════

    // POST: /Appointment/JoinWaitlist (AJAX)
    [HttpPost]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> JoinWaitlist([FromBody] JoinWaitlistRequest request)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Json(new { success = false, message = "Unauthorized" });

        if (string.IsNullOrWhiteSpace(patient.User.FullName) ||
            string.IsNullOrWhiteSpace(patient.Phone) ||
            string.IsNullOrWhiteSpace(patient.CitizenId))
        {
            return Json(new { success = false, message = "Vui lòng cập nhật đầy đủ thông tin cá nhân (Họ tên, Ngày sinh, Giới tính, SĐT, CCCD) trong Hồ sơ cá nhân trước khi tham gia danh sách chờ." });
        }

        var (success, message) = await _appointmentService.JoinWaitlistAsync(
            patient.Id, request.DoctorId, request.WatchDate);
        return Json(new { success, message });
    }

    // POST: /Appointment/LeaveWaitlist (AJAX)
    [HttpPost]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> LeaveWaitlist([FromBody] LeaveWaitlistRequest request)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Json(new { success = false, message = "Unauthorized" });

        var result = await _appointmentService.RemoveFromWaitlistAsync(request.WaitlistId, patient.Id);
        return Json(new { success = result });
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

public class RescheduleViewModel
{
    public int AppointmentId { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string? RoomNumber { get; set; }
    public DateTime CurrentSlotStart { get; set; }
    public DateTime CurrentSlotEnd { get; set; }
}

public class CancelDirectRequest
{
    public int AppointmentId { get; set; }
}

public class JoinWaitlistRequest
{
    public int DoctorId { get; set; }
    public DateOnly WatchDate { get; set; }
}

public class LeaveWaitlistRequest
{
    public int WaitlistId { get; set; }
}
