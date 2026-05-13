using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class AiInsight
{
    public Guid InsightId { get; set; }

    public Guid PatientId { get; set; }

    public string? PredictedDisease { get; set; }

    public decimal? RiskPercentage { get; set; }

    public string? AiAdvice { get; set; }

    public string? ModelVersion { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}
