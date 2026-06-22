namespace SmartHealthMonitoring.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        int? targetUserId = null,
        string? targetName = null);

    Task LogForActorAsync(
        int? actorUserId,
        string? actorName,
        string? actorEmail,
        string action,
        string entityName,
        string? entityId,
        string description,
        int? targetUserId = null,
        string? targetName = null,
        string? ipAddress = null,
        string? userAgent = null);
}
