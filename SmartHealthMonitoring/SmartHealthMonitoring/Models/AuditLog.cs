using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class AuditLog
{
    public Guid LogId { get; set; }

    public Guid UserId { get; set; }

    public string Action { get; set; } = null!;

    public string TableName { get; set; } = null!;

    public string RecordId { get; set; } = null!;

    public DateTime? Timestamp { get; set; }

    public virtual User User { get; set; } = null!;
}
