using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

public class DoctorWorkSchedule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required]
    [Range(0, 6)]
    public byte DayOfWeek { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [Required]
    [Range(10, 120)]
    public int SlotDurationMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

    public virtual Doctor Doctor { get; set; } = null!;
}
