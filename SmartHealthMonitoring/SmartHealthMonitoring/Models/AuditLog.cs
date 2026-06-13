namespace SmartHealthMonitoring.Models;

public partial class AuditLog
{
    public int Id { get; set; }

    public int? ActorUserId { get; set; }

    public string ActorName { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public int? TargetUserId { get; set; }

    public string? TargetName { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ActorUser { get; set; }

    public virtual User? TargetUser { get; set; }
}
