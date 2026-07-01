using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Hubs;

namespace SmartHealthMonitoring.Services;

/// <summary>
/// Core business logic cho luồng đặt lịch.
/// Xử lý Race Condition bằng Optimistic Concurrency (EF Core [Timestamp] RowVersion).
/// </summary>
public class AppointmentService : IAppointmentService
{
    private readonly SmartHealthMonitoringContext _context;
    private readonly ILogger<AppointmentService> _logger;
    private readonly IHubContext<AppointmentHub> _hubContext;

    public AppointmentService(
        SmartHealthMonitoringContext context,
        ILogger<AppointmentService> logger,
        IHubContext<AppointmentHub> hubContext)
    {
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
    }

    // ═══════════════════════════════════════════════════════════════
    // QUERY
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int doctorId, DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.AppointmentSlots
            .Where(s =>
                s.DoctorId == doctorId &&
                s.SlotStart >= dayStart &&
                s.SlotStart <= dayEnd &&
                (s.Status == AppointmentSlotStatus.Available ||
                 // Slot SoftLocked đã hết hạn - vẫn hiện là trống
                 (s.Status == AppointmentSlotStatus.SoftLocked && s.SoftLockedUntil < DateTime.UtcNow)))
            .OrderBy(s => s.SlotStart)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetPatientAppointmentsAsync(int patientId)
    {
        return await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.Slot.SlotStart)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetDoctorQueueAsync(int doctorId, DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.Appointments
            .Include(a => a.Slot)
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Where(a =>
                a.DoctorId == doctorId &&
                a.Slot.SlotStart >= dayStart &&
                a.Slot.SlotStart <= dayEnd &&
                (a.Status == AppointmentStatus.Confirmed))
            .OrderBy(a => a.Slot.SlotStart)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    // BOOKING FLOW
    // ═══════════════════════════════════════════════════════════════

    public async Task<(bool success, string message)> SoftLockSlotAsync(int slotId, int patientId)
    {
        var slot = await _context.AppointmentSlots.FindAsync(slotId);
        if (slot == null)
            return (false, "Slot không tồn tại.");

        // Nếu slot đang SoftLocked bởi người khác và chưa hết hạn
        if (slot.Status == AppointmentSlotStatus.SoftLocked
            && slot.SoftLockedUntil > DateTime.UtcNow
            && slot.PatientId != patientId)
            return (false, "Khung giờ này đang được người khác giữ chỗ. Vui lòng thử lại sau ít phút.");

        if (slot.Status == AppointmentSlotStatus.Booked)
            return (false, "Khung giờ này đã có người đặt.");

        if (slot.Status == AppointmentSlotStatus.Blocked)
            return (false, "Bác sĩ đã chặn khung giờ này.");

        // Giữ chỗ 5 phút
        slot.Status = AppointmentSlotStatus.SoftLocked;
        slot.PatientId = patientId;
        slot.SoftLockedUntil = DateTime.UtcNow.AddMinutes(5);

        try
        {
            await _context.SaveChangesAsync();
            return (true, "Đã giữ chỗ trong 5 phút. Vui lòng hoàn tất đặt lịch.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Khung giờ này vừa được người khác chọn. Vui lòng chọn giờ khác.");
        }
    }

    /// <summary>
    /// ⭐ CORE: Đặt lịch với Optimistic Concurrency.
    /// EF Core so sánh RowVersion trước khi UPDATE. Nếu mismatch → DbUpdateConcurrencyException.
    /// </summary>
    public async Task<(bool success, string message, Appointment? appointment)> BookSlotAsync(
        int slotId, int patientId, string? note)
    {
        // Lấy slot - KHÔNG dùng AsNoTracking để EF theo dõi RowVersion
        var slot = await _context.AppointmentSlots
            .FirstOrDefaultAsync(s => s.Id == slotId);

        if (slot == null)
            return (false, "Slot không tồn tại.", null);

        // Kiểm tra trạng thái trước khi đặt
        bool isOwnSoftLock = slot.Status == AppointmentSlotStatus.SoftLocked
                          && slot.PatientId == patientId
                          && slot.SoftLockedUntil >= DateTime.UtcNow;

        if (slot.Status == AppointmentSlotStatus.Booked)
            return (false, "Khung giờ này đã có người đặt. Vui lòng chọn giờ khác!", null);

        if (slot.Status == AppointmentSlotStatus.Blocked)
            return (false, "Bác sĩ đã chặn khung giờ này.", null);

        if (slot.Status == AppointmentSlotStatus.SoftLocked && !isOwnSoftLock)
            return (false, "Khung giờ này đang được người khác giữ chỗ. Vui lòng thử lại sau ít phút.", null);

        // Cập nhật slot → Booked
        slot.Status      = AppointmentSlotStatus.Booked;
        slot.PatientId   = patientId;
        slot.SoftLockedUntil = null;

        // Tạo bản ghi Appointment
        var appointment = new Appointment
        {
            SlotId      = slotId,
            PatientId   = patientId,
            DoctorId    = slot.DoctorId,
            Status      = AppointmentStatus.Confirmed,
            PatientNote = note,
            CreatedAt   = DateTime.UtcNow
        };
        _context.Appointments.Add(appointment);

        try
        {
            // ⭐ EF Core tự động so sánh RowVersion trong WHERE clause của UPDATE.
            // Nếu người khác đã thay đổi slot, EF sẽ throw DbUpdateConcurrencyException.
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

    // ═══════════════════════════════════════════════════════════════
    // CANCEL / COMPLETE / BLOCK
    // ═══════════════════════════════════════════════════════════════

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

        // Đổi trạng thái lịch hẹn
        appointment.Status    = isDoctor ? AppointmentStatus.CancelledByDoctor : AppointmentStatus.CancelledByPatient;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Nhả slot về Available
        appointment.Slot.Status    = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;

        await _context.SaveChangesAsync();
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
        appointment.UpdatedAt       = DateTime.UtcNow;
        appointment.Slot.Status     = AppointmentSlotStatus.Completed;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task BlockTimeAsync(int doctorId, DateTime blockStart, DateTime blockEnd, string? reason)
    {
        // Lấy tất cả slot của bác sĩ trong khoảng thời gian block
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

    public async Task<(bool success, string message, Appointment? appointment)> CreatePendingAppointmentAsync(
        int slotId, int patientId, string? note)
    {
        var slot = await _context.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot == null)
            return (false, "Slot không tồn tại.", null);

        if (slot.Status == AppointmentSlotStatus.Booked)
            return (false, "Khung giờ này đã có người đặt.", null);

        if (slot.Status == AppointmentSlotStatus.Blocked)
            return (false, "Bác sĩ đã chặn khung giờ này.", null);

        if (slot.Status == AppointmentSlotStatus.SoftLocked && slot.PatientId != patientId && slot.SoftLockedUntil > DateTime.UtcNow)
            return (false, "Khung giờ này đang được người khác giữ chỗ. Vui lòng thử lại sau ít phút.", null);

        slot.Status = AppointmentSlotStatus.SoftLocked;
        slot.PatientId = patientId;
        slot.SoftLockedUntil = DateTime.MaxValue; // SoftLock vĩnh viễn chờ duyệt

        var appointment = new Appointment
        {
            SlotId = slotId,
            PatientId = patientId,
            DoctorId = slot.DoctorId,
            Status = AppointmentStatus.Pending,
            PatientNote = note,
            CreatedAt = DateTime.UtcNow
        };
        _context.Appointments.Add(appointment);

        try
        {
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("SlotStatusChanged", slotId, "SoftLocked");
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
        appointment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Appointment>> GetPendingAppointmentsAsync()
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
        appointment.UpdatedAt = DateTime.UtcNow;

        appointment.Slot.Status = AppointmentSlotStatus.Booked;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("SlotBooked", appointment.SlotId);
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
        appointment.UpdatedAt = DateTime.UtcNow;

        appointment.Slot.Status = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", appointment.SlotId, "Available");
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
        appointment.UpdatedAt = DateTime.UtcNow;

        appointment.Slot.Status = AppointmentSlotStatus.Available;
        appointment.Slot.PatientId = null;
        appointment.Slot.SoftLockedUntil = null;

        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("SlotStatusChanged", appointment.SlotId, "Available");
        return true;
    }

    public async Task<bool> RejectAppointmentCancellationAsync(int appointmentId)
    {
        var appointment = await _context.Appointments.FindAsync(appointmentId);
        if (appointment == null || appointment.Status != AppointmentStatus.CancellationPending)
            return false;

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
