using SmartHealthMonitoring.ViewModels.Admin;

namespace SmartHealthMonitoring.Interfaces.Audit;

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

    Task<(List<AuditLogListItemViewModel> Items, int TotalCount)> GetFilteredLogsAsync(
        string? actionType, string? entityName, int? actorUserId,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        
    Task<List<AuditLogActorOptionViewModel>> GetActorOptionsAsync();
}
