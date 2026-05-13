using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class MetricType
{
    public int MetricTypeId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public virtual ICollection<GlobalThreshold> GlobalThresholds { get; set; } = new List<GlobalThreshold>();

    public virtual ICollection<HealthMetric> HealthMetrics { get; set; } = new List<HealthMetric>();
}
