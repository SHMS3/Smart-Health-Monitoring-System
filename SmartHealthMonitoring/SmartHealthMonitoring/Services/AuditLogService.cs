using System.Security.Claims;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services;

public class AuditLogService : IAuditLogService
{
    private readonly SmartHealthMonitoringContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        SmartHealthMonitoringContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        int? targetUserId = null,
        string? targetName = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        int? actorUserId = null;
        var actorIdClaim = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(actorIdClaim, out var parsedActorId))
        {
            actorUserId = parsedActorId;
        }

        var auditLog = CreateAuditLog(
                actorUserId,
                user?.FindFirstValue("FullName") ?? user?.Identity?.Name,
                user?.FindFirstValue(ClaimTypes.Email),
                action,
                entityName,
                entityId,
                description,
                targetUserId,
                targetName,
                httpContext?.Connection.RemoteIpAddress?.ToString(),
                httpContext?.Request.Headers.UserAgent.ToString());

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task LogForActorAsync(
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
        string? userAgent = null)
    {
        var auditLog = CreateAuditLog(
            actorUserId,
            actorName,
            actorEmail,
            action,
            entityName,
            entityId,
            description,
            targetUserId,
            targetName,
            ipAddress,
            userAgent);

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    private static AuditLog CreateAuditLog(
        int? actorUserId,
        string? actorName,
        string? actorEmail,
        string action,
        string entityName,
        string? entityId,
        string description,
        int? targetUserId,
        string? targetName,
        string? ipAddress,
        string? userAgent)
    {
        return new AuditLog
        {
            ActorUserId = actorUserId,
            ActorName = Truncate(actorName ?? "Unknown", 100),
            ActorEmail = Truncate(actorEmail ?? string.Empty, 150),
            Action = Truncate(action, 50),
            EntityName = Truncate(entityName, 100),
            EntityId = TruncateNullable(entityId, 64),
            TargetUserId = targetUserId,
            TargetName = TruncateNullable(targetName, 100),
            Description = Truncate(description, 1000),
            IpAddress = TruncateNullable(ipAddress, 45),
            UserAgent = TruncateNullable(userAgent, 512),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength];
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
