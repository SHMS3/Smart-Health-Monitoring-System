using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class EmailNotification
{
    public int Id { get; set; }

    public int AlertId { get; set; }

    public int PatientId { get; set; }

    public string Subject { get; set; } = null!;

    public string Body { get; set; } = null!;

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual WarningAlert Alert { get; set; } = null!;

    public virtual Patient Patient { get; set; } = null!;
}
