using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

/// <summary>
/// Một ô lịch cụ thể (slot) được sinh tự động từ DoctorWorkSchedule.
/// Chứa [Timestamp] RowVersion để EF Core thực hiện Optimistic Concurrency.
/// </summary>
public class AppointmentSlot
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required]
    public DateTime SlotStart { get; set; }

    [Required]
    public DateTime SlotEnd { get; set; }

    [Required]
    public AppointmentSlotStatus Status { get; set; } = AppointmentSlotStatus.Available;

    /// <summary>
    /// Concurrency Token - EF Core tự quản lý.
    /// Khi 2 người cùng đặt 1 slot, người thứ 2 sẽ bị DbUpdateConcurrencyException.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>Id bệnh nhân đang giữ chỗ tạm / đã đặt thành công</summary>
    public int? PatientId { get; set; }

    /// <summary>Thời điểm hết hạn giữ chỗ tạm (SoftLock), sau đó slot về Available</summary>
    public DateTime? SoftLockedUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Doctor Doctor { get; set; } = null!;
    public virtual Patient? Patient { get; set; }
    public virtual Appointment? Appointment { get; set; }
}

public enum AppointmentSlotStatus
{
    /// <summary>Còn trống, bệnh nhân có thể đặt</summary>
    Available = 0,

    /// <summary>Đang bị giữ tạm thời bởi một bệnh nhân (5 phút)</summary>
    SoftLocked = 1,

    /// <summary>Đã được đặt thành công</summary>
    Booked = 2,

    /// <summary>Bị bác sĩ block (nghỉ phép, hội họp...)</summary>
    Blocked = 3,

    /// <summary>Cuộc hẹn đã hoàn tất</summary>
    Completed = 4
}
