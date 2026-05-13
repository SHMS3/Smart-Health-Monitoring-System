using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class MedicalRecord
{
    public Guid RecordId { get; set; }

    public Guid PatientId { get; set; }

    public Guid DoctorId { get; set; }

    public Guid? AppointmentId { get; set; }

    public string? Symptoms { get; set; }

    public string? Diagnosis { get; set; }

    public string? PrescriptionNote { get; set; }

    public string? Status { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual ICollection<LabResult> LabResults { get; set; } = new List<LabResult>();

    public virtual Patient Patient { get; set; } = null!;
}
