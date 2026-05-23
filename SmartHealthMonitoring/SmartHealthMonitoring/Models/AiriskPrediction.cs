using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class AiriskPrediction
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public int? ClinicalRecordId { get; set; }

    public int? DailyLogId { get; set; }

    public decimal RiskScore { get; set; }

    public byte PredictedTarget { get; set; }

    public byte RiskLevel { get; set; }

    public string ModelVersion { get; set; } = null!;

    public DateTime PredictedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ClinicalRecord? ClinicalRecord { get; set; }

    public virtual DailyVitalLog? DailyLog { get; set; }

    public virtual Patient Patient { get; set; } = null!;

    public virtual WarningAlert? WarningAlert { get; set; }
}
