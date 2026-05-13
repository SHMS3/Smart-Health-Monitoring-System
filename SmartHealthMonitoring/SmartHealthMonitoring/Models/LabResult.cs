using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class LabResult
{
    public Guid LabId { get; set; }

    public Guid RecordId { get; set; }

    public string TestName { get; set; } = null!;

    public string? ResultFileUrl { get; set; }

    public string? DoctorNotes { get; set; }

    public DateTime? UploadedAt { get; set; }

    public virtual MedicalRecord Record { get; set; } = null!;
}
