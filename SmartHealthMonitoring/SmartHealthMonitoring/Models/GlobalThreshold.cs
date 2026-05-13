using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class GlobalThreshold
{
    public int ThresholdId { get; set; }

    public int MetricTypeId { get; set; }

    public int MinAge { get; set; }

    public int MaxAge { get; set; }

    public decimal SafeMinValue { get; set; }

    public decimal SafeMaxValue { get; set; }

    public string SeverityLevel { get; set; } = null!;

    public string WarningMessage { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual MetricType MetricType { get; set; } = null!;
}
