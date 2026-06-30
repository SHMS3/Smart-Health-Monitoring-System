using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

/// <summary>
/// Lịch làm việc cố định hàng tuần của bác sĩ.
/// Mỗi bản ghi = 1 ca làm việc trong 1 ngày trong tuần.
/// </summary>
public class DoctorWorkSchedule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DoctorId { get; set; }

    /// <summary>0=CN, 1=T2, 2=T3, 3=T4, 4=T5, 5=T6, 6=T7</summary>
    [Required]
    [Range(0, 6)]
    public byte DayOfWeek { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    /// <summary>Thời lượng mỗi slot, mặc định 30 phút</summary>
    [Required]
    [Range(10, 120)]
    public int SlotDurationMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Doctor Doctor { get; set; } = null!;
}
