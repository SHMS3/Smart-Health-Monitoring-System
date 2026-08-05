using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

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

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public int? PatientId { get; set; }

    public DateTime? SoftLockedUntil { get; set; }

    public DateTime CreatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

    public virtual Doctor Doctor { get; set; } = null!;
    public virtual Patient? Patient { get; set; }
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public enum AppointmentSlotStatus
{
    Available = 0,

    SoftLocked = 1,

    Booked = 2,

    Blocked = 3,

    Completed = 4
}
