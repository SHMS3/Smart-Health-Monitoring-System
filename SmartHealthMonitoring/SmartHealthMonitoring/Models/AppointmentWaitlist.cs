using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartHealthMonitoring.Models;

public class AppointmentWaitlist
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PatientId { get; set; }

    [Required]
    public int DoctorId { get; set; }

    [Required]
    public DateOnly WatchDate { get; set; }

    public bool IsNotified { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = SmartHealthMonitoring.Common.AppTime.Now;

    public DateTime? NotifiedAt { get; set; }

    public virtual Patient Patient { get; set; } = null!;
    public virtual Doctor Doctor { get; set; } = null!;
}
