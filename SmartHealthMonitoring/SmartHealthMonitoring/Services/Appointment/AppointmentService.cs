using SmartHealthMonitoring.Interfaces.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Appointment;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Hubs;

namespace SmartHealthMonitoring.Services.Appointment;

public class AppointmentService : IAppointmentService
{
    private readonly SmartHealthMonitoringContext _context;
    private readonly ILogger<AppointmentService> _logger;
    private readonly IHubContext<AppointmentHub> _hubContext;
    private readonly IEmailService _emailService;

    private const int MinCancelHours = 1;

    public AppointmentService(
        SmartHealthMonitoringContext context,
        ILogger<AppointmentService> logger,
        IHubContext<AppointmentHub> hubContext,
        IEmailService emailService)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
        _emailService = emailService;
    }


    public async Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int doctorId, DateOnly date, int? currentPatientId = null)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var dayEnd   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

        var now = DateTime.UtcNow;
        return await _context.AppointmentSlots
            .Where(s =>
                s.DoctorId == doctorId &&
                s.SlotStart >= dayStart &&
                s.SlotStart < dayEnd &&
                s.SlotStart > now &&
                (s.Status == AppointmentSlotStatus.Available ||
                 (s.Status == AppointmentSlotStatus.SoftLocked && s.SoftLockedUntil < SmartHealthMonitoring.Common.AppTime.Now) ||
                 (s.Status == AppointmentSlotStatus.SoftLocked && currentPatientId.HasValue && s.PatientId == currentPatientId.Value)))
            .OrderBy(s => s.SlotStart)
            .ToListAsync();
    }

    public async Task<List<AppointmentSlot>> GetAvailableSlotsRangeAsync(int doctorId, DateOnly startDate, DateOnly endDate, int? currentPatientId = null)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var end   = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

        var now = DateTime.UtcNow;
        return await _context.AppointmentSlots
            .Where(s =>
                s.DoctorId == doctorId &&
                s.SlotStart >= start &&
                s.SlotStart < end &&
                s.SlotStart > now &&
                (s.Status == AppointmentSlotStatus.Available ||
                 (s.Status == AppointmentSlotStatus.SoftLocked && s.SoftLockedUntil < SmartHealthMonitoring.Common.AppTime.Now) ||
                 (s.Status == AppointmentSlotStatus.SoftLocked && currentPatientId.HasValue && s.PatientId == currentPatientId.Value)))
            .OrderBy(s => s.SlotStart)
            .ToListAsync();
    }

    public async Task<List<AppointmentSlot>> GetAvailableSlotsRangeForDoctorsAsync(List<int> doctorIds, DateOnly startDate, DateOnly endDate, int? currentPatientId = null)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var end   = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);
        var now   = DateTime.Now;

        return await _context.AppointmentSlots
            .Where(s =>
                doctorIds.Contains(s.DoctorId) &&
                s.SlotStart >= start &&
                s.SlotStart >= now &&
                s.SlotStart <= end &&
                s.SlotStart > now &&
                (s.Status == AppointmentSlotStatus.Available ||
                 (s.Status == AppointmentSlotStatus.SoftLocked && s.SoftLockedUntil < SmartHealthMonitoring.Common.AppTime.Now) ||
                 (s.Status == AppointmentSlotStatus.SoftLocked && currentPatientId.HasValue && s.PatientId == currentPatientId.Value)))
            .OrderBy(s => s.SlotStart)
            .ToListAsync();
    }


    public async Task<List<SmartHealthMonitoring.Models.Appointment>> GetPatientAppointmentsAsync(int patientId)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.Slot.SlotStart)
            .ToListAsync();
    }

    public async Task<List<SmartHealthMonitoring.Models.Appointment>> GetDoctorQueueAsync(int doctorId, DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var dayEnd   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local);

        return await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a =>
                a.DoctorId == doctorId &&
                a.Slot.SlotStart >= dayStart &&
                a.Slot.SlotStart < dayEnd &&
                (a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.Slot.SlotStart)
            .ToListAsync();
    }


    public async Task<(bool success, string message)> SoftLockSlotAsync(int slotId, int patientId)
    {
        var slot = await _context.AppointmentSlots.FindAsync(slotId);
        if (slot == null)
            return (false, "Slot không tồn tại.");

        if (slot.Status == AppointmentSlotStatus.SoftLocked
            && slot.SoftLockedUntil > SmartHealthMonitoring.Common.AppTime.Now
            && slot.PatientId != patientId)
            return (false, "Khung giờ này đang được người khác giữ chỗ. Vui lòng thử lại sau ít phút.");

        if (slot.Status == AppointmentSlotStatus.Booked)
            return (false, "Khung giờ này đã có người đặt.");

        if (slot.Status == AppointmentSlotStatus.Blocked)
            return (false, "Bác sĩ đã chặn khung giờ này.");

        slot.Status = AppointmentSlotStatus.SoftLocked;
        slot.PatientId = patientId;
        slot.SoftLockedUntil = SmartHealthMonitoring.Common.AppTime.Now.AddMinutes(10);

        try
        {
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("SlotStatusChanged", slotId, "SoftLocked");
            return (true, "Đã giữ chỗ trong 10 phút. Vui lòng hoàn tất đặt lịch.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Khung giờ này vừa được người khác chọn. Vui lòng chọn giờ khác.");
        }
    }

    public async Task<(bool success, string message, SmartHealthMonitoring.Models.Appointment? appointment)> BookSlotAsync(
        int slotId, int patientId, string? note)
    {
        var slot = await _context.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
            return (false, "Slot không tồn tại.", null);

        var hasActiveOrPending = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patientId &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
        if (hasActiveOrPending)
            return (false, "Bạn đang có lịch khám chưa hoàn thành hoặc yêu cầu đặt lịch đang chờ duyệt. Không thể đặt thêm lịch mới.", null);

        bool isOwnSoftLock = slot.Status == AppointmentSlotStatus.SoftLocked
                          && slot.PatientId == patientId
                          && slot.SoftLockedUntil >= SmartHealthMonitoring.Common.AppTime.Now;

        if (slot.Status == AppointmentSlotStatus.Booked)
            return (false, "Khung giờ này đã có người đặt. Vui lòng chọn giờ khác!", null);

        if (slot.Status == AppointmentSlotStatus.Blocked)
            return (false, "Bác sĩ đã chặn khung giờ này.", null);

        if (slot.Status == AppointmentSlotStatus.SoftLocked && !isOwnSoftLock)
            return (false, "Khung giờ này đang được người khác giữ chỗ. Vui lòng thử lại sau ít phút.", null);

        slot.Status      = AppointmentSlotStatus.Booked;
        slot.PatientId   = patientId;
        slot.SoftLockedUntil = null;

        var appointment = new SmartHealthMonitoring.Models.Appointment
        {
            SlotId      = slotId,
            PatientId   = patientId,
            DoctorId    = slot.DoctorId,
            Status      = AppointmentStatus.Confirmed,
            PatientNote = note,
            CreatedAt   = SmartHealthMonitoring.Common.AppTime.Now
        };
        _context.Appointments.Add(appointment);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Patient {PatientId} booked slot {SlotId} successfully.", patientId, slotId);
            return (true, "Đặt lịch thành công!", appointment);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Race condition: Patient {PatientId} lost slot {SlotId} to another user.", patientId, slotId);
            return (false, "Rất tiếc! Khung giờ này vừa được người khác đặt mất trong tích tắc. Vui lòng chọn giờ khác.", null);
        }
    }


    public async Task<(bool success, string message)> CancelAppointmentAsync(
        int appointmentId, int userId, bool isDoctor)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null)
            return (false, "Lịch hẹn không tồn tại.");

        if (appointment.Status == AppointmentStatus.Completed)
            return (false, "Không thể huỷ lịch hẹn đã hoàn thành.");

        appointment.Status    = isDoctor ? AppointmentStatus.CancelledByDoctor : AppointmentStatus.CancelledByPatient;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        var slotId = appointment.Slot.Id;
        var doctorId = appointment.Slot.DoctorId;
        var slotDate = DateOnly.FromDateTime(appointment.Slot.SlotStart);
        appointment.Slot.Status    = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", slotId, "Available");
        await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId,
            isDoctor ? "CancelledByDoctor" : "CancelledByPatient");

        _ = Task.Run(async () =>
        {
            try { await NotifyWaitlistSubscribersAsync(doctorId, slotDate); }
            catch (Exception ex) { _logger.LogWarning(ex, "Waitlist notify failed after cancel."); }
        });

        return (true, "Đã huỷ lịch hẹn thành công.");
    }

    public async Task<bool> CompleteAppointmentAsync(int appointmentId, int clinicalRecordId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null) return false;

        appointment.Status          = AppointmentStatus.Completed;
        appointment.ClinicalRecordId = clinicalRecordId;
        appointment.UpdatedAt       = SmartHealthMonitoring.Common.AppTime.Now;
        appointment.Slot.Status     = AppointmentSlotStatus.Completed;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task BlockTimeAsync(int doctorId, DateTime blockStart, DateTime blockEnd, string? reason)
    {
        var slotsToBlock = await _context.AppointmentSlots
            .Where(s =>
                s.DoctorId == doctorId &&
                s.SlotStart >= blockStart &&
                s.SlotStart < blockEnd &&
                s.Status == AppointmentSlotStatus.Available)
            .ToListAsync();

        foreach (var slot in slotsToBlock)
            slot.Status = AppointmentSlotStatus.Blocked;

        await _context.SaveChangesAsync();
    }

    public async Task<bool> ReleaseSoftLockSlotAsync(int slotId, int patientId)
    {
        var slot = await _context.AppointmentSlots.FindAsync(slotId);
        if (slot != null && slot.Status == AppointmentSlotStatus.SoftLocked && slot.PatientId == patientId)
        {
            slot.Status          = AppointmentSlotStatus.Available;
            slot.PatientId       = null;
            slot.SoftLockedUntil = null;
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("SlotStatusChanged", slotId, "Available");
            return true;
        }
        return false;
    }

    public async Task<(bool success, string message, SmartHealthMonitoring.Models.Appointment? appointment)> CreatePendingAppointmentAsync(
        int slotId, int patientId, string? note)
    {
        var slot = await _context.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot == null)
            return (false, "Slot không tồn tại.", null);

        var hasActiveOrPending = await _context.Appointments.AnyAsync(a =>
            a.PatientId == patientId &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
        if (hasActiveOrPending)
            return (false, "Bạn đang có lịch khám chưa hoàn thành hoặc yêu cầu đặt lịch đang chờ duyệt. Không thể đặt thêm lịch mới.", null);

        if (slot.Status == AppointmentSlotStatus.Booked)
            return (false, "Khung giờ này đã có người đặt.", null);

        if (slot.Status == AppointmentSlotStatus.Blocked)
            return (false, "Bác sĩ đã chặn khung giờ này.", null);

        if (slot.Status == AppointmentSlotStatus.SoftLocked && slot.PatientId != patientId && slot.SoftLockedUntil > SmartHealthMonitoring.Common.AppTime.Now)
            return (false, "Khung giờ này đang được người khác giữ chỗ. Vui lòng thử lại sau ít phút.", null);

        slot.Status = AppointmentSlotStatus.SoftLocked;
        slot.PatientId = patientId;
        slot.SoftLockedUntil = DateTime.MaxValue; // SoftLock vĩnh viễn chờ duyệt

        var appointment = new global::SmartHealthMonitoring.Models.Appointment
        {
            SlotId = slotId,
            PatientId = patientId,
            DoctorId = slot.DoctorId,
            Status = AppointmentStatus.Pending,
            PatientNote = note,
            CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
        };
        _context.Appointments.Add(appointment);

        try
        {
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("SlotStatusChanged", slotId, "SoftLocked");

            var fullAppt = await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Slot)
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointment.Id);

            if (fullAppt != null)
            {
                Console.WriteLine($"[AppointmentService] Broadcasting NewBookingRequest to group 'Staff' for appt #{fullAppt.Id}");
                await _hubContext.Clients.Group("Staff").SendAsync("NewBookingRequest", new
                {
                    appointmentId  = fullAppt.Id,
                    patientName    = fullAppt.Patient.User.FullName,
                    patientPhone   = fullAppt.Patient.Phone ?? "",
                    patientEmail   = fullAppt.Patient.User.Email,
                    doctorName     = fullAppt.Doctor.User.FullName,
                    specialty      = fullAppt.Doctor.Specialty,
                    slotStart      = fullAppt.Slot.SlotStart.ToString("HH:mm"),
                    slotEnd        = fullAppt.Slot.SlotEnd.ToString("HH:mm"),
                    slotDate       = fullAppt.Slot.SlotStart.ToString("dd/MM/yyyy"),
                    patientNote    = fullAppt.PatientNote ?? ""
                });
                Console.WriteLine($"[AppointmentService] Broadcast done.");
            }

            return (true, "Yêu cầu đặt lịch hẹn đã được gửi thành công, vui lòng chờ duyệt.", appointment);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Khung giờ này vừa được người khác chọn. Vui lòng chọn giờ khác.", null);
        }
    }

    public async Task<bool> RequestCancelAppointmentAsync(int appointmentId, string reason)
    {
        var appointment = await _context.Appointments.FindAsync(appointmentId);

        if (appointment == null || appointment.Status != AppointmentStatus.Confirmed)
            return false;

        appointment.Status = AppointmentStatus.CancellationPending;
        appointment.PatientNote = (appointment.PatientNote ?? "") + "\n[Yêu cầu huỷ]: " + reason;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        await _context.SaveChangesAsync();

        var fullAppt = await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (fullAppt != null)
        {
            Console.WriteLine($"[AppointmentService] Broadcasting NewCancellationRequest to group 'Staff' for appt #{fullAppt.Id}");
            await _hubContext.Clients.Group("Staff").SendAsync("NewCancellationRequest", new
            {
                appointmentId  = fullAppt.Id,
                patientName    = fullAppt.Patient.User.FullName,
                patientPhone   = fullAppt.Patient.Phone ?? "",
                patientEmail   = fullAppt.Patient.User.Email,
                doctorName     = fullAppt.Doctor.User.FullName,
                specialty      = fullAppt.Doctor.Specialty,
                slotStart      = fullAppt.Slot.SlotStart.ToString("HH:mm"),
                slotEnd        = fullAppt.Slot.SlotEnd.ToString("HH:mm"),
                slotDate       = fullAppt.Slot.SlotStart.ToString("dd/MM/yyyy"),
                patientNote    = fullAppt.PatientNote ?? ""
            });
        }

        return true;
    }

    public async Task<List<SmartHealthMonitoring.Models.Appointment>> GetPendingAppointmentsAsync()
    {
        return await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.CancellationPending)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ApproveAppointmentBookingAsync(int appointmentId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null || appointment.Status != AppointmentStatus.Pending)
            return false;

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        appointment.Slot.Status = AppointmentSlotStatus.Booked;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("SlotBooked", appointment.SlotId);
        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", appointment.SlotId, "Booked");
        await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId, "Confirmed");
        return true;
    }

    public async Task<bool> RejectAppointmentBookingAsync(int appointmentId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null || appointment.Status != AppointmentStatus.Pending)
            return false;

        appointment.Status = AppointmentStatus.CancelledByDoctor;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        appointment.Slot.Status = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", appointment.SlotId, "Available");
        await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId, "CancelledByDoctor");
        return true;
    }

    public async Task<bool> ApproveAppointmentCancellationAsync(int appointmentId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

        if (appointment == null || appointment.Status != AppointmentStatus.CancellationPending)
            return false;

        appointment.Status = AppointmentStatus.CancelledByPatient;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        appointment.Slot.Status = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", appointment.SlotId, "Available");
        await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId, "CancelledByPatient");
        return true;
    }

    public async Task<bool> RejectAppointmentCancellationAsync(int appointmentId)
    {
        var appointment = await _context.Appointments.FindAsync(appointmentId);
        if (appointment == null || appointment.Status != AppointmentStatus.CancellationPending)
            return false;

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId, "Confirmed");
        return true;
    }


    public async Task<(bool success, string message)> CancelDirectAsync(int appointmentId, int patientId)
    {
        var appointment = await _context.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);

        if (appointment == null)
            return (false, "Lịch hẹn không tồn tại hoặc không thuộc về bạn.");

        if (appointment.Status != AppointmentStatus.Confirmed)
            return (false, "Chỉ có thể huỷ lịch hẹn đã xác nhận.");

        var hoursUntilAppt = (appointment.Slot.SlotStart - SmartHealthMonitoring.Common.AppTime.Now).TotalHours;
        if (hoursUntilAppt < MinCancelHours)
            return (false, $"Không thể huỷ trực tiếp khi còn dưới {MinCancelHours} giờ. Vui lòng gửi yêu cầu huỷ qua bộ phận hỗ trợ.");

        appointment.Status = AppointmentStatus.CancelledByPatient;
        appointment.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

        var slotId = appointment.Slot.Id;
        var doctorId = appointment.Slot.DoctorId;
        var slotDate = DateOnly.FromDateTime(appointment.Slot.SlotStart);

        appointment.Slot.Status = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Patient {PatientId} cancelled appointment {AppointmentId} directly.", patientId, appointmentId);

        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", slotId, "Available");
        await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId, "CancelledByPatient");

        _ = Task.Run(async () =>
        {
            try { await NotifyWaitlistSubscribersAsync(doctorId, slotDate); }
            catch (Exception ex) { _logger.LogWarning(ex, "Waitlist notify failed after direct cancel."); }
        });

        return (true, "Đã huỷ lịch hẹn thành công.");
    }


    public async Task<(bool success, string message, SmartHealthMonitoring.Models.Appointment? newAppointment)> RescheduleAppointmentAsync(
        int appointmentId, int newSlotId, int patientId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var oldAppt = await _context.Appointments
                    .Include(a => a.Slot)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);

                if (oldAppt == null)
                    return (false, "Lịch hẹn không tồn tại hoặc không thuộc về bạn.", null);

                if (oldAppt.Status != AppointmentStatus.Confirmed)
                    return (false, "Chỉ có thể dời lịch hẹn đã xác nhận.", null);

                var hoursUntilAppt = (oldAppt.Slot.SlotStart - SmartHealthMonitoring.Common.AppTime.Now).TotalHours;
                if (hoursUntilAppt < MinCancelHours)
                    return (false, $"Không thể dời lịch khi còn dưới {MinCancelHours} giờ trước giờ hẹn.", null);

                var newSlot = await _context.AppointmentSlots
                    .FirstOrDefaultAsync(s => s.Id == newSlotId);

                if (newSlot == null)
                    return (false, "Slot mới không tồn tại.", null);

                if (newSlot.DoctorId != oldAppt.DoctorId)
                    return (false, "Chỉ có thể dời lịch trong cùng bác sĩ.", null);

                bool isOwnSoftLock = newSlot.Status == AppointmentSlotStatus.SoftLocked
                                  && newSlot.PatientId == patientId
                                  && newSlot.SoftLockedUntil >= SmartHealthMonitoring.Common.AppTime.Now;

                if (newSlot.Status == AppointmentSlotStatus.Booked)
                    return (false, "Khung giờ mới đã có người đặt.", null);

                if (newSlot.Status == AppointmentSlotStatus.Blocked)
                    return (false, "Bác sĩ đã chặn khung giờ mới.", null);

                if (newSlot.Status == AppointmentSlotStatus.SoftLocked && !isOwnSoftLock)
                    return (false, "Khung giờ mới đang được người khác giữ chỗ.", null);

                var oldSlotId = oldAppt.Slot.Id;
                var oldSlotDate = DateOnly.FromDateTime(oldAppt.Slot.SlotStart);

                oldAppt.Status = AppointmentStatus.CancelledByPatient;
                oldAppt.DoctorNote = "[Hệ thống] Bệnh nhân dời lịch sang slot mới.";
                oldAppt.UpdatedAt = SmartHealthMonitoring.Common.AppTime.Now;

                oldAppt.Slot.Status = AppointmentSlotStatus.Available;
                oldAppt.Slot.PatientId = null;
                oldAppt.Slot.SoftLockedUntil = null;

                newSlot.Status = AppointmentSlotStatus.SoftLocked;
                newSlot.PatientId = patientId;
                newSlot.SoftLockedUntil = DateTime.MaxValue; // SoftLock vĩnh viễn chờ duyệt

                var newAppt = new global::SmartHealthMonitoring.Models.Appointment
                {
                    SlotId = newSlotId,
                    PatientId = patientId,
                    DoctorId = newSlot.DoctorId,
                    Status = AppointmentStatus.Pending,
                    PatientNote = oldAppt.PatientNote,
                    CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
                };
                _context.Appointments.Add(newAppt);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Patient {PatientId} rescheduled appointment {OldId} → new slot {NewSlotId}.",
                    patientId, appointmentId, newSlotId);

                await _hubContext.Clients.All.SendAsync("SlotStatusChanged", oldSlotId, "Available");
                await _hubContext.Clients.All.SendAsync("SlotStatusChanged", newSlotId, "SoftLocked");
                await _hubContext.Clients.All.SendAsync("AppointmentStatusChanged", appointmentId, "CancelledByPatient");

                var fullAppt = await _context.Appointments
                    .AsNoTracking()
                    .Include(a => a.Slot)
                    .Include(a => a.Patient).ThenInclude(p => p.User)
                    .Include(a => a.Doctor).ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(a => a.Id == newAppt.Id);

                if (fullAppt != null)
                {
                    await _hubContext.Clients.Group("Staff").SendAsync("NewBookingRequest", new
                    {
                        appointmentId = fullAppt.Id,
                        patientName = fullAppt.Patient.User.FullName,
                        patientPhone = fullAppt.Patient.Phone ?? "",
                        patientEmail = fullAppt.Patient.User.Email,
                        doctorName = fullAppt.Doctor.User.FullName,
                        specialty = fullAppt.Doctor.Specialty,
                        slotStart = fullAppt.Slot.SlotStart.ToString("HH:mm"),
                        slotEnd = fullAppt.Slot.SlotEnd.ToString("HH:mm"),
                        slotDate = fullAppt.Slot.SlotStart.ToString("dd/MM/yyyy"),
                        patientNote = "[Dời lịch] " + (fullAppt.PatientNote ?? "")
                    });
                }

                _ = Task.Run(async () =>
                {
                    try { await NotifyWaitlistSubscribersAsync(oldAppt.DoctorId, oldSlotDate); }
                    catch { /* ignore */ }
                });

                return (true, "Dời lịch thành công! Vui lòng chờ nhân viên duyệt.", newAppt);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return (false, "Khung giờ mới vừa được người khác đặt. Vui lòng chọn giờ khác.", null);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }


    public async Task<(bool success, string message)> JoinWaitlistAsync(int patientId, int doctorId, DateOnly watchDate)
    {
        var exists = await _context.AppointmentWaitlists.AnyAsync(w =>
            w.PatientId == patientId && w.DoctorId == doctorId && w.WatchDate == watchDate && w.IsActive);

        if (exists)
            return (false, "Bạn đã đăng ký theo dõi ngày này rồi.");

        _context.AppointmentWaitlists.Add(new AppointmentWaitlist
        {
            PatientId = patientId,
            DoctorId = doctorId,
            WatchDate = watchDate,
            IsActive = true,
            CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
        });

        await _context.SaveChangesAsync();
        return (true, "Đã đăng ký nhận thông báo khi có slot trống.");
    }

    public async Task<List<AppointmentWaitlist>> GetPatientWaitlistAsync(int patientId)
    {
        return await _context.AppointmentWaitlists
            .AsNoTracking()
            .Include(w => w.Doctor).ThenInclude(d => d.User)
            .Where(w => w.PatientId == patientId && w.IsActive && w.WatchDate >= DateOnly.FromDateTime(SmartHealthMonitoring.Common.AppTime.Now))
            .OrderBy(w => w.WatchDate)
            .ToListAsync();
    }

    public async Task<bool> RemoveFromWaitlistAsync(int waitlistId, int patientId)
    {
        var entry = await _context.AppointmentWaitlists
            .FirstOrDefaultAsync(w => w.Id == waitlistId && w.PatientId == patientId);

        if (entry == null) return false;

        entry.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task NotifyWaitlistSubscribersAsync(int doctorId, DateOnly date)
    {
        var subscribers = await _context.AppointmentWaitlists
            .Include(w => w.Patient).ThenInclude(p => p.User)
            .Include(w => w.Doctor).ThenInclude(d => d.User)
            .Where(w => w.DoctorId == doctorId && w.WatchDate == date && w.IsActive && !w.IsNotified)
            .ToListAsync();

        foreach (var sub in subscribers)
        {
            try
            {
                var subject = "Thông báo: Đã có slot trống cho lịch hẹn bạn quan tâm — SmartHealth";
                var body = $@"
                    <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;border:1px solid #e1e8ed;border-radius:8px;overflow:hidden'>
                        <div style='background:linear-gradient(135deg,#8b5cf6,#a78bfa);color:white;padding:24px;text-align:center'>
                            <h2 style='margin:0;font-size:20px'>🔔 ĐÃ CÓ SLOT TRỐNG</h2>
                        </div>
                        <div style='padding:24px'>
                            <p>Kính chào <strong>{sub.Patient.User.FullName}</strong>,</p>
                            <p>Chúng tôi vui mừng thông báo: Đã có slot trống cho bác sĩ <strong>{sub.Doctor.User.FullName}</strong> ({sub.Doctor.Specialty}) vào ngày <strong>{date:dd/MM/yyyy}</strong>.</p>
                            <p>Hãy nhanh tay đặt lịch trước khi hết chỗ!</p>
                            <div style='text-align:center;margin:24px 0'>
                                <a href='/Appointment/FindDoctor' style='display:inline-block;background:#8b5cf6;color:white;padding:12px 28px;border-radius:30px;text-decoration:none;font-weight:600'>Đặt lịch ngay</a>
                            </div>
                        </div>
                        <div style='background:#f1f5f9;padding:16px;text-align:center;font-size:12px;color:#64748b'>
                            Đây là email tự động từ Hệ thống Y tế SmartHealth.
                        </div>
                    </div>";

                await _emailService.SendEmailAsync(sub.Patient.User.Email, subject, body);

                sub.IsNotified = true;
                sub.NotifiedAt = SmartHealthMonitoring.Common.AppTime.Now;
                sub.IsActive = false; // Tự tắt sau khi thông báo
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify waitlist subscriber {Id}.", sub.Id);
            }
        }

        if (subscribers.Any())
            await _context.SaveChangesAsync();
    }
    public async Task<AppointmentSlot?> GetSlotByIdAsync(int slotId)
    {
        return await _context.AppointmentSlots
            .Include(s => s.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.Id == slotId);
    }

    public async Task<bool> HasActiveOrPendingAppointmentAsync(int patientId)
    {
        return await _context.Appointments.AnyAsync(a =>
            a.PatientId == patientId &&
            (a.Status == AppointmentStatus.Confirmed || 
             a.Status == AppointmentStatus.Pending || 
             a.Status == AppointmentStatus.CancellationPending));
    }

    public async Task<SmartHealthMonitoring.Models.Appointment?> GetAppointmentByIdAndPatientAsync(int appointmentId, int patientId)
    {
        return await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);
    }

    public async Task<List<SmartHealthMonitoring.Models.Appointment>> GetDoctorCalendarAppointmentsAsync(int doctorId, DateTime startDate, DateTime endDate)
    {
        return await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a => a.Slot.DoctorId == doctorId
                     && a.Slot.SlotStart >= startDate
                     && a.Slot.SlotStart < endDate
                     && a.Status == AppointmentStatus.Confirmed)
            .OrderBy(a => a.Slot.SlotStart)
            .ToListAsync();
    }

    public async Task<List<WaitingPatient>> GetDoctorWaitingQueueAsync(int doctorId, DateTime date)
    {
        return await _context.WaitingPatients
            .Include(w => w.Patient).ThenInclude(p => p.User)
            .Where(w => w.CreatedAt >= date
                     && w.DoctorId == doctorId
                     && (w.Status == 0 || w.Status == 1))
            .OrderBy(w => w.SequenceNumber)
            .ToListAsync();
    }

    public async Task<List<int>> GetPatientPaymentsAsync(List<int> patientIds, DateTime date, string status)
    {
        return await _context.Payments
            .Where(p => patientIds.Contains(p.PatientId) && p.CreatedAt.Date == date && p.Status == status)
            .Select(p => p.PatientId).Distinct().ToListAsync();
    }
}


