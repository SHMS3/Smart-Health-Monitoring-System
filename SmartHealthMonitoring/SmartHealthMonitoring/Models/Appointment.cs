using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Appointment
{
    public Guid AppointmentId { get; set; }

    public Guid SlotId { get; set; }

    public Guid PatientId { get; set; }

    public string? SymptomsNote { get; set; }

    public string Status { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();

    public virtual Patient Patient { get; set; } = null!;

    public virtual AppointmentSlot Slot { get; set; } = null!;
}
