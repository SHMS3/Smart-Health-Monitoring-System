using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Doctor
{
    public Guid DoctorId { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Specialty { get; set; }

    public string? LicenseNumber { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<DoctorSchedule> DoctorSchedules { get; set; } = new List<DoctorSchedule>();

    public virtual ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public virtual User User { get; set; } = null!;
}
