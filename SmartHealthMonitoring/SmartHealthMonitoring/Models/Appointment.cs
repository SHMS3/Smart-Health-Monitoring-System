using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

/// <summary>
/// Lịch hẹn được tạo sau khi bệnh nhân đặt thành công.
/// Một Appointment tương ứng với một AppointmentSlot đã được Booked.
/// </summary>
public class Appointment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SlotId { get; set; }

    [Required]
    public int PatientId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

    /// <summary>Ghi chú triệu chứng của bệnh nhân khi đặt lịch</summary>
    [StringLength(1000)]
    public string? PatientNote { get; set; }

    /// <summary>Ghi chú của bác sĩ khi duyệt / từ chối / hoàn tất</summary>
    [StringLength(1000)]
    public string? DoctorNote { get; set; }

    /// <summary>Liên kết hồ sơ bệnh án khi khám xong (BOOK-10)</summary>
    public int? ClinicalRecordId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public virtual AppointmentSlot Slot { get; set; } = null!;
    public virtual Patient Patient { get; set; } = null!;
    public virtual Doctor Doctor { get; set; } = null!;
    public virtual ClinicalRecord? ClinicalRecord { get; set; }
}

public enum AppointmentStatus
{
    /// <summary>Đã xác nhận (auto-confirm sau khi đặt thành công)</summary>
    Confirmed = 0,

    /// <summary>Đã hoàn thành khám, hồ sơ đã được tạo</summary>
    Completed = 1,

    /// <summary>Bệnh nhân huỷ</summary>
    CancelledByPatient = 2,

    /// <summary>Bác sĩ huỷ</summary>
    CancelledByDoctor = 3,

    /// <summary>Bệnh nhân không đến (hệ thống tự đánh dấu)</summary>
    NoShow = 4,

    /// <summary>Chờ duyệt đặt lịch</summary>
    Pending = 5,

    /// <summary>Chờ duyệt huỷ lịch</summary>
    CancellationPending = 6
}
