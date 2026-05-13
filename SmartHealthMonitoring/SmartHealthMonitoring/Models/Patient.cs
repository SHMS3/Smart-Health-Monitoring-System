using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Patient
{
    public Guid PatientId { get; set; }

    public Guid? UserId { get; set; }

    public string FullName { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public string Gender { get; set; } = null!;

    public string? BloodType { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContact { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<AiInsight> AiInsights { get; set; } = new List<AiInsight>();

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<HealthMetric> HealthMetrics { get; set; } = new List<HealthMetric>();

    public virtual ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public virtual User? User { get; set; }
}
