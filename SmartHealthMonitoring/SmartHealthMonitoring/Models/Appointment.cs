using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

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

    [StringLength(1000)]
    public string? PatientNote { get; set; }

    [StringLength(1000)]
    public string? DoctorNote { get; set; }

    public int? ClinicalRecordId { get; set; }

    public DateTime CreatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

    public DateTime? UpdatedAt { get; set; }

    public bool IsReminded24h { get; set; }

    public bool IsReminded2h { get; set; }

    public virtual AppointmentSlot Slot { get; set; } = null!;
    public virtual Patient Patient { get; set; } = null!;
    public virtual Doctor Doctor { get; set; } = null!;
    public virtual ClinicalRecord? ClinicalRecord { get; set; }
}

public enum AppointmentStatus
{
    Confirmed = 0,

    Completed = 1,

    CancelledByPatient = 2,

    CancelledByDoctor = 3,

    NoShow = 4,

    Pending = 5,

    CancellationPending = 6
}
