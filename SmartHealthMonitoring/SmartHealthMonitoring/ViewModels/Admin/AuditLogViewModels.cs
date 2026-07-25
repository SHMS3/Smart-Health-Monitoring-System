using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.ViewModels.Admin;

public class AuditLogIndexViewModel
{
    public string? ActionType { get; set; }

    public string? EntityName { get; set; }

    public int? ActorUserId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public List<AuditLogActorOptionViewModel> Actors { get; set; } = new();

    public PagedResult<AuditLogListItemViewModel> Logs { get; set; } = new();
}

public class AuditLogActorOptionViewModel
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public byte Role { get; set; }

    public bool IsDeleted { get; set; }
}

public class AuditLogListItemViewModel
{
    public int Id { get; set; }

    public string ActorName { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? TargetName { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }
}
