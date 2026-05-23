using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Doctor
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Specialty { get; set; } = null!;

    public bool IsOnShift { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<ClinicalRecord> ClinicalRecords { get; set; } = new List<ClinicalRecord>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<WarningAlert> WarningAlerts { get; set; } = new List<WarningAlert>();
}
