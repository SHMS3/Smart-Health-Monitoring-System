using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

/// <summary>
/// SCH-07: Hàng đợi chờ — Bệnh nhân đăng ký nhận thông báo
/// khi một ngày nào đó (đã hết chỗ) có slot trống.
/// </summary>
public class AppointmentWaitlist
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    /// <summary>Ngày bệnh nhân muốn theo dõi slot trống</summary>
    [Required]
    public DateOnly WatchDate { get; set; }

    /// <summary>Đã gửi thông báo chưa</summary>
    public bool IsNotified { get; set; }

    /// <summary>Còn active không (false nếu bệnh nhân huỷ hoặc đã đặt được)</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? NotifiedAt { get; set; }

    // Navigation
    public virtual Patient Patient { get; set; } = null!;
    public virtual Doctor Doctor { get; set; } = null!;
}
