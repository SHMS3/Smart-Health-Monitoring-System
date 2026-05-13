using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class Alert
{
    public Guid AlertId { get; set; }

    public Guid PatientId { get; set; }

    public Guid MetricId { get; set; }

    public string Severity { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsResolved { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual HealthMetric Metric { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;
}
