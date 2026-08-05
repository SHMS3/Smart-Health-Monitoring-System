using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SmartHealthMonitoring.Interfaces.Appointment;
using SmartHealthMonitoring.Interfaces.Doctor;
using SmartHealthMonitoring.Interfaces.Patient;
using SmartHealthMonitoring.Interfaces.Email;
using SmartHealthMonitoring.Models;
using System.Security.Claims;
using SmartHealthMonitoring.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers;

[Authorize]
public class AppointmentController : Controller
{
    private readonly IAppointmentService _appointmentService;
    private readonly IEmailService _emailService;
    private readonly IDoctorService _doctorService;
    private readonly IProfileService _profileService;

    public AppointmentController(
        IAppointmentService appointmentService,
        IEmailService emailService,
        IDoctorService doctorService,
        IProfileService profileService)
    {
        _appointmentService = appointmentService;
        _emailService = emailService;
        _doctorService = doctorService;
        _profileService = profileService;
    }

    private async Task<(Patient? patient, Doctor? doctor)> GetCurrentUserAsync()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role   = User.FindFirstValue(ClaimTypes.Role);

        Patient? patient = role == "0" ? await _profileService.GetPatientByUserIdAsync(userId) : null;
        Doctor? doctor = role == "1" ? await _doctorService.GetDoctorByUserIdAsync(userId) : null;

        return (patient, doctor);
    }

    [Authorize(Roles = "0")]
    public async Task<IActionResult> FindDoctor(string? specialty, string? doctorName, DateOnly? fromDate, DateOnly? toDate, byte? gender, string? session, string? roomNumber)
    {
        var (patient, _) = await GetCurrentUserAsync();
        var patientId = patient?.Id;

        var startDate = fromDate ?? DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
        var endDate = toDate ?? startDate.AddDays(6);
        if (endDate < startDate) endDate = startDate;

        var doctors = await _doctorService.GetAllFilteredDoctorsAsync(specialty, doctorName, gender, roomNumber);
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
        
        ViewBag.Specialties  = await _doctorService.GetDistinctSpecialtiesAsync();
        ViewBag.RoomNumbers  = await _doctorService.GetDistinctRoomNumbersAsync();

        return View(doctorSlotsData);
    }

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
            TempData["Error"] = "Vui l�ng c?p nh?t d?y d? th�ng tin c� nh�n (H? t�n, Ng�y sinh, Gi?i t�nh, S�T, CCCD) trong H? so c� nh�n tru?c khi d?t l?ch.";
            return RedirectToAction("Profile", "Home");
        }

        var slot = await _appointmentService.GetSlotByIdAsync(slotId);
        if (slot == null) return NotFound();

        var hasActiveOrPending = await _appointmentService.HasActiveOrPendingAppointmentAsync(patient.Id);
        if (hasActiveOrPending)
        {
            TempData["Error"] = "B?n dang c� l?ch kh�m chua ho�n th�nh ho?c y�u c?u d?t l?ch dang ch? duy?t. Kh�ng th? d?t th�m l?ch m?i.";
            return RedirectToAction(nameof(FindDoctor));
        }

        var (lockSuccess, lockMessage) = await _appointmentService.SoftLockSlotAsync(slotId, patient.Id);
        if (!lockSuccess)
        {
            TempData["Error"] = lockMessage;
            return RedirectToAction(nameof(FindDoctor));
        }

        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString($"BookingOtp_{slotId}", otp);
        HttpContext.Session.SetString($"BookingOtpTime_{slotId}", SmartHealthMonitoring.Common.AppTime.Now.ToString("o"));

        var subject = "M� OTP x�c th?c d?t l?ch kh�m b?nh - SmartHealth";
        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 8px; max-width: 600px;'>
                <h2 style='color: #27ae60;'>X�c th?c d?t l?ch kh�m</h2>
                <p>K�nh ch�o qu� kh�ch <strong>{patient.User.FullName}</strong>,</p>
                <p>B?n dang ti?n h�nh d?t l?ch kh�m chuy�n khoa <strong>{slot.Doctor.Specialty}</strong> v?i b�c si <strong>{slot.Doctor.User.FullName}</strong>.</p>
                <p>M� OTP x�c th?c c?a b?n l�:</p>
                <div style='background-color: #f7f9fa; padding: 15px; border-radius: 6px; text-align: center; font-size: 24px; font-weight: bold; color: #27ae60; letter-spacing: 5px; margin: 20px 0;'>
                    {otp}
                </div>
                <p style='color: #888; font-size: 12px;'>M� OTP n�y c� hi?u l?c trong 10 ph�t. N?u b?n kh�ng th?c hi?n y�u c?u n�y, vui l�ng b? qua email.</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='font-size: 12px; color: #aaa;'>H? th?ng Y t? SmartHealth - �?ng h�nh c�ng s?c kh?e c?a b?n.</p>
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
            catch { }
        });

        ViewBag.SlotId = slotId;
        ViewBag.DoctorName = slot.Doctor.User.FullName;
        ViewBag.Specialty = slot.Doctor.Specialty;
        ViewBag.SlotStart = slot.SlotStart;
        ViewBag.SlotEnd = slot.SlotEnd;

        return View();
    }

    [HttpPost]
    [Authorize(Roles = "0")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(int slotId, string otpCode)
    {
        var storedOtp = HttpContext.Session.GetString($"BookingOtp_{slotId}");
        var storedOtpTimeStr = HttpContext.Session.GetString($"BookingOtpTime_{slotId}");

        var (patient, _) = await GetCurrentUserAsync();
        var slot = await _appointmentService.GetSlotByIdAsync(slotId);

        if (slot == null) return NotFound();

        var hasActiveOrPending = await _appointmentService.HasActiveOrPendingAppointmentAsync(patient.Id);
        if (hasActiveOrPending)
        {
            TempData["Error"] = "B?n dang c� l?ch kh�m chua ho�n th�nh ho?c y�u c?u d?t l?ch dang ch? duy?t. Kh�ng th? d?t th�m l?ch m?i.";
            return RedirectToAction(nameof(FindDoctor));
        }

        ViewBag.SlotId = slotId;
        ViewBag.DoctorName = slot.Doctor.User.FullName;
        ViewBag.Specialty = slot.Doctor.Specialty;
        ViewBag.SlotStart = slot.SlotStart;
        ViewBag.SlotEnd = slot.SlotEnd;

        if (string.IsNullOrEmpty(storedOtp) || string.IsNullOrEmpty(storedOtpTimeStr) || storedOtp != otpCode.Trim())
        {
            ModelState.AddModelError("", "M� OTP kh�ng ch�nh x�c.");
            return View();
        }

        if (DateTime.TryParse(storedOtpTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var otpTime))
        {
            if (SmartHealthMonitoring.Common.AppTime.Now - otpTime > TimeSpan.FromMinutes(10))
            {
                ModelState.AddModelError("", "M� OTP d� qu� h?n 10 ph�t. Vui l�ng quay l?i trang ch?n b�c si d? nh?n m� m?i.");
                return View();
            }
        }

        HttpContext.Session.SetString($"BookingOtpVerified_{slotId}", "true");
        return RedirectToAction(nameof(Book), new { slotId = slotId });
    }

    [Authorize(Roles = "0")]
    public async Task<IActionResult> Book(int slotId)
    {
        var verified = HttpContext.Session.GetString($"BookingOtpVerified_{slotId}");
        if (verified != "true")
        {
            TempData["Error"] = "Vui l�ng x�c th?c m� OTP tru?c khi d?t l?ch.";
            return RedirectToAction(nameof(FindDoctor));
        }

        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var hasActiveOrPending = await _appointmentService.HasActiveOrPendingAppointmentAsync(patient.Id);
        if (hasActiveOrPending)
        {
            TempData["Error"] = "B?n dang c� l?ch kh�m chua ho�n th�nh ho?c y�u c?u d?t l?ch dang ch? duy?t. Kh�ng th? d?t th�m l?ch m?i.";
            return RedirectToAction(nameof(FindDoctor));
        }

        var slot = await _appointmentService.GetSlotByIdAsync(slotId);

        if (slot == null) return NotFound();

        if (slot.Status == AppointmentSlotStatus.Booked)
        {
            TempData["Error"] = "Khung gi? n�y d� c� ngu?i d?t. Vui l�ng ch?n gi? kh�c!";
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> ConfirmBook(int slotId, string? patientNote)
    {
        var verified = HttpContext.Session.GetString($"BookingOtpVerified_{slotId}");
        if (verified != "true")
        {
            TempData["Error"] = "Vui l�ng x�c th?c m� OTP tru?c khi d?t l?ch.";
            return RedirectToAction(nameof(FindDoctor));
        }

        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var hasActiveOrPending = await _appointmentService.HasActiveOrPendingAppointmentAsync(patient.Id);
        if (hasActiveOrPending)
        {
            TempData["Error"] = "B?n dang c� l?ch kh�m chua ho�n th�nh ho?c y�u c?u d?t l?ch dang ch? duy?t. Kh�ng th? d?t th�m l?ch m?i.";
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

        var slot = await _appointmentService.GetSlotByIdAsync(slotId);

        var subject = $"Th�ng tin y�u c?u d?t l?ch kh�m - SmartHealth";
        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #e1e8ed; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #27ae60; color: white; padding: 24px; text-align: center;'>
                    <h2 style='margin: 0; font-size: 20px;'>Y�U C?U �?T L?CH H?N KH�M</h2>
                </div>
                <div style='padding: 24px;'>
                    <p><strong>K�nh g?i Qu� kh�ch h�ng {patient.User.FullName},</strong></p>
                    <p>H? th?ng Y t? SmartHealth d� nh?n du?c y�u c?u v? l?ch h?n kh�m c?a qu� kh�ch. T?ng d�i vi�n c?a ch�ng t�i s? li�n h? l?i qua s? di?n tho?i c?a qu� kh�ch d? x�c nh?n th�ng tin.</p>
                    <p>Qu� kh�ch vui l�ng d? � di?n tho?i v� ch? nh�n vi�n li�n h? x�c nh?n ch�nh x�c l?ch h?n kh�m.</p>
                    
                    <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 16px; margin: 20px 0;'>
                        <h3 style='margin-top: 0; color: #1e293b; font-size: 16px; border-bottom: 1px solid #e2e8f0; padding-bottom: 8px;'>Chi ti?t y�u c?u l?ch kh�m</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 14px;'>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b; width: 140px;'>B�c si:</td>
                                <td style='padding: 6px 0; font-weight: bold;'>BS. {slot.Doctor.User.FullName}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Chuy�n khoa:</td>
                                <td>{slot.Doctor.Specialty}</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Th?i gian kh�m:</td>
                                <td style='font-weight: bold; color: #27ae60;'>{slot.SlotStart:HH:mm} - {slot.SlotEnd:HH:mm} (Ng�y {slot.SlotStart:dd/MM/yyyy})</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>Ph�ng kh�m:</td>
                                <td style='font-weight: bold; color: #92400e;'>{(slot.Doctor.RoomNumber ?? "Chua ph�n ph�ng")} � H? th?ng Y t? SmartHealth</td>
                            </tr>
                            <tr>
                                <td style='padding: 6px 0; color: #64748b;'>L� do kh�m:</td>
                                <td>{patientNote ?? "Kh�ng c�"}</td>
                            </tr>
                        </table>
                    </div>
                    
                    <p>C?m on qu� kh�ch d� l?a ch?n d?ch v? c?a SmartHealth!</p>
                </div>
                <div style='background-color: #f1f5f9; padding: 16px; text-align: center; font-size: 12px; color: #64748b; border-top: 1px solid #e2e8f0;'>
                    <p style='margin: 0;'>��y l� email t? d?ng, vui l�ng kh�ng ph?n h?i email n�y.</p>
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
            catch { }
        });

        return RedirectToAction(nameof(BookingConfirmation), new { appointmentId = appointment.Id });
    }

    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> BookingConfirmation(int appointmentId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _appointmentService.GetAppointmentByIdAndPatientAsync(appointmentId, patient.Id);

        if (appointment == null) return NotFound();

        return View(appointment);
    }

    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> SupportPortal(int appointmentId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _appointmentService.GetAppointmentByIdAndPatientAsync(appointmentId, patient.Id);

        if (appointment == null) return NotFound();

        return View(appointment);
    }

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
            TempData["Error"] = "Kh�ng th? g?i y�u c?u h?y cho l?ch h?n n�y.";
            return RedirectToAction(nameof(MyAppointments));
        }

        TempData["Success"] = "Y�u c?u h?y l?ch d� du?c g?i th�nh c�ng, nh�n vi�n s? li�n h? x�c nh?n s?m.";
        return RedirectToAction(nameof(MyAppointments));
    }

    [Authorize(Roles = "0")]
    public async Task<IActionResult> MyAppointments()
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointments = await _appointmentService.GetPatientAppointmentsAsync(patient.Id);

        ViewBag.WaitlistItems = await _appointmentService.GetPatientWaitlistAsync(patient.Id);

        return View(appointments);
    }

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

    [Authorize(Roles = "1")]
    public async Task<IActionResult> DoctorQueue(DateOnly? date)
    {
        var (_, doctor) = await GetCurrentUserAsync();
        if (doctor == null) return Forbid();

        var selectedDate = date ?? DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now);
        var todayUtc = SmartHealthMonitoring.Common.AppTime.Now.Date;
        var endDate = todayUtc.AddDays(30);

        var allAppointments = await _appointmentService.GetDoctorCalendarAppointmentsAsync(doctor.Id, todayUtc, endDate);

        var byDate = allAppointments
            .GroupBy(a => DateOnly.FromDateTime(a.Slot.SlotStart.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        var waitingQueue = new List<WaitingPatient>();
        if (selectedDate == DateOnly.FromDateTime(todayUtc))
        {
            waitingQueue = await _appointmentService.GetDoctorWaitingQueueAsync(doctor.Id, todayUtc);
        }

        var queue = allAppointments
            .Where(a => DateOnly.FromDateTime(a.Slot.SlotStart.Date) == selectedDate)
            .Where(a => !waitingQueue.Any(w => w.PatientId == a.PatientId))
            .Where(a => a.Status != AppointmentStatus.Completed)
            .ToList();

        var patientIds = waitingQueue.Select(w => w.PatientId).ToList();
        patientIds.AddRange(queue.Select(a => a.PatientId));
        patientIds = patientIds.Distinct().ToList();
        
        var paidPayments = await _appointmentService.GetPatientPaymentsAsync(patientIds, todayUtc, "Paid");
        var pendingPayments = await _appointmentService.GetPatientPaymentsAsync(patientIds, todayUtc, "Pending");
        
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "1")]
    public async Task<IActionResult> Complete(int appointmentId, int clinicalRecordId)
    {
        await _appointmentService.CompleteAppointmentAsync(appointmentId, clinicalRecordId);
        TempData["Success"] = "�� ho�n t?t l?ch h?n v� li�n k?t h? so b?nh �n.";
        return RedirectToAction(nameof(DoctorQueue));
    }

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

    [HttpPost]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> CancelDirect([FromBody] CancelDirectRequest request)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Json(new { success = false, message = "Unauthorized" });

        var (success, message) = await _appointmentService.CancelDirectAsync(request.AppointmentId, patient.Id);
        return Json(new { success, message });
    }

    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> Reschedule(int appointmentId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _appointmentService.GetAppointmentByIdAndPatientAsync(appointmentId, patient.Id);

        if (appointment == null) return NotFound();

        if (appointment.Status != SmartHealthMonitoring.Models.AppointmentStatus.Confirmed)
        {
            TempData["Error"] = "Ch? c� th? d?i l?ch h?n d� x�c nh?n.";
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

    [HttpGet]
    [Authorize(Roles = "0")]
    public async Task<IActionResult> RescheduleVerifyOtp(int appointmentId, int newSlotId)
    {
        var (patient, _) = await GetCurrentUserAsync();
        if (patient == null) return Forbid();

        var appointment = await _appointmentService.GetAppointmentByIdAndPatientAsync(appointmentId, patient.Id);

        if (appointment == null) return NotFound();

        var newSlot = await _appointmentService.GetSlotByIdAsync(newSlotId);
        if (newSlot == null) return NotFound();

        var (lockSuccess, lockMessage) = await _appointmentService.SoftLockSlotAsync(newSlotId, patient.Id);
        if (!lockSuccess)
        {
            TempData["Error"] = lockMessage;
            return RedirectToAction(nameof(Reschedule), new { appointmentId });
        }

        var otp = new Random().Next(100000, 999999).ToString();
        HttpContext.Session.SetString($"RescheduleOtp_{appointmentId}_{newSlotId}", otp);
        HttpContext.Session.SetString($"RescheduleOtpTime_{appointmentId}_{newSlotId}", SmartHealthMonitoring.Common.AppTime.Now.ToString("o"));

        var subject = "M� OTP x�c th?c d?i l?ch kh�m - SmartHealth";
        var emailBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #eee; border-radius: 8px; max-width: 600px;'>
                <h2 style='color: #6366f1;'>X�c th?c d?i l?ch kh�m</h2>
                <p>K�nh ch�o <strong>{patient.User.FullName}</strong>,</p>
                <p>B?n dang d?i l?ch h?n kh�m <strong>{appointment.Doctor.Specialty}</strong> v?i BS. <strong>{appointment.Doctor.User.FullName}</strong>.</p>
                <p>T?: <strong>{appointment.Slot.SlotStart:HH:mm dd/MM/yyyy}</strong> ? Sang: <strong>{newSlot.SlotStart:HH:mm dd/MM/yyyy}</strong></p>
                <p>M� OTP x�c th?c:</p>
                <div style='background-color: #f7f9fa; padding: 15px; border-radius: 6px; text-align: center; font-size: 24px; font-weight: bold; color: #6366f1; letter-spacing: 5px; margin: 20px 0;'>
                    {otp}
                </div>
                <p style='color: #888; font-size: 12px;'>M� OTP c� hi?u l?c trong 10 ph�t.</p>
            </div>";

        var toEmail = patient.User.Email;
        var serviceScopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();
            try { await emailSvc.SendEmailAsync(toEmail, subject, emailBody); }
            catch { }
        });

        ViewBag.AppointmentId = appointmentId;
        ViewBag.NewSlotId = newSlotId;
        ViewBag.DoctorName = appointment.Doctor.User.FullName;
        ViewBag.Specialty = appointment.Doctor.Specialty;
        ViewBag.OldSlot = $"{appointment.Slot.SlotStart:HH:mm} � {appointment.Slot.SlotEnd:HH:mm}, {appointment.Slot.SlotStart:dd/MM/yyyy}";
        ViewBag.NewSlot = $"{newSlot.SlotStart:HH:mm} � {newSlot.SlotEnd:HH:mm}, {newSlot.SlotStart:dd/MM/yyyy}";

        return View();
    }

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
        var appointment = await _appointmentService.GetAppointmentByIdAndPatientAsync(appointmentId, patient!.Id);
        var newSlot = await _appointmentService.GetSlotByIdAsync(newSlotId);

        if (appointment == null || newSlot == null) return NotFound();

        ViewBag.AppointmentId = appointmentId;
        ViewBag.NewSlotId = newSlotId;
        ViewBag.DoctorName = appointment.Doctor.User.FullName;
        ViewBag.Specialty = appointment.Doctor.Specialty;
        ViewBag.OldSlot = $"{appointment.Slot.SlotStart:HH:mm} � {appointment.Slot.SlotEnd:HH:mm}, {appointment.Slot.SlotStart:dd/MM/yyyy}";
        ViewBag.NewSlot = $"{newSlot.SlotStart:HH:mm} � {newSlot.SlotEnd:HH:mm}, {newSlot.SlotStart:dd/MM/yyyy}";

        if (string.IsNullOrEmpty(storedOtp) || storedOtp != otpCode?.Trim())
        {
            ModelState.AddModelError("", "M� OTP kh�ng ch�nh x�c.");
            return View();
        }

        if (DateTime.TryParse(storedOtpTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var otpTime))
        {
            if (SmartHealthMonitoring.Common.AppTime.Now - otpTime > TimeSpan.FromMinutes(10))
            {
                ModelState.AddModelError("", "M� OTP d� h?t h?n. Vui l�ng th? l?i.");
                return View();
            }
        }

        var (success, message, newAppt) = await _appointmentService.RescheduleAppointmentAsync(
            appointmentId, newSlotId, patient!.Id);

        HttpContext.Session.Remove(key);
        HttpContext.Session.Remove(timeKey);

        if (!success || newAppt == null)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(MyAppointments));
        }

        var emailSubject = "Th�ng tin d?i l?ch h?n kh�m - SmartHealth";
        var emailBody = $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e1e8ed;border-radius:8px;overflow:hidden'>
                <div style='background:linear-gradient(135deg,#6366f1,#818cf8);color:white;padding:24px;text-align:center'>
                    <h2 style='margin:0'>D?I L?CH H?N KH�M TH�NH C�NG</h2>
                </div>
                <div style='padding:24px'>
                    <p>K�nh g?i <strong>{patient.User.FullName}</strong>,</p>
                    <p>L?ch h?n c?a b?n d� du?c d?i th�nh c�ng. Vui l�ng ch? nh�n vi�n x�c nh?n.</p>
                    <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:16px;margin:16px 0'>
                        <p style='margin:4px 0'><strong>B�c si:</strong> BS. {appointment.Doctor.User.FullName}</p>
                        <p style='margin:4px 0'><strong>L?ch cu:</strong> <del>{appointment.Slot.SlotStart:HH:mm} - {appointment.Slot.SlotEnd:HH:mm}, {appointment.Slot.SlotStart:dd/MM/yyyy}</del></p>
                        <p style='margin:4px 0;color:#16a34a'><strong>L?ch m?i:</strong> {newSlot.SlotStart:HH:mm} - {newSlot.SlotEnd:HH:mm}, {newSlot.SlotStart:dd/MM/yyyy}</p>
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
            catch { }
        });

        TempData["Success"] = message;
        return RedirectToAction(nameof(MyAppointments));
    }

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
            return Json(new { success = false, message = "Vui l�ng c?p nh?t d?y d? th�ng tin c� nh�n (H? t�n, Ng�y sinh, Gi?i t�nh, S�T, CCCD) trong H? so c� nh�n tru?c khi tham gia danh s�ch ch?." });
        }

        var (success, message) = await _appointmentService.JoinWaitlistAsync(
            patient.Id, request.DoctorId, request.WatchDate);
        return Json(new { success, message });
    }

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

public class DoctorSlotViewModel
{
    public SmartHealthMonitoring.Models.Doctor Doctor { get; set; } = null!;
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
