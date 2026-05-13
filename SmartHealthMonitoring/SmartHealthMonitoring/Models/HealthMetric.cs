using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class HealthMetric
{
    public Guid MetricId { get; set; }

    public Guid PatientId { get; set; }

    public int MetricTypeId { get; set; }

    public decimal Value { get; set; }

    public string? Notes { get; set; }

    public DateTime MeasuredAt { get; set; }

    public string Source { get; set; } = null!;

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual MetricType MetricType { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;
}
