using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using SmartHealthMonitoring.Hubs;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Context;

namespace SmartHealthMonitoring.Services;

public class AuditLogService : IAuditLogService
{
    private readonly SmartHealthMonitoringContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHubContext<AuditLogHub> _auditLogHubContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        SmartHealthMonitoringContext context,
        IHttpContextAccessor httpContextAccessor,
        IHubContext<AuditLogHub> auditLogHubContext,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _auditLogHubContext = auditLogHubContext;
        _logger = logger;
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

        await LogForActorAsync(
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
        await BroadcastAuditLogAsync(auditLog);
    }

    private async Task BroadcastAuditLogAsync(AuditLog auditLog)
    {
        try
        {
            var payload = new
            {
                id = auditLog.Id,
                actorUserId = auditLog.ActorUserId,
                actorName = auditLog.ActorName,
                actorEmail = auditLog.ActorEmail,
                action = auditLog.Action,
                entityName = auditLog.EntityName,
                entityId = auditLog.EntityId,
                targetName = auditLog.TargetName,
                description = auditLog.Description,
                ipAddress = auditLog.IpAddress,
                createdAt = auditLog.CreatedAt.ToString("O")
            };

            await _auditLogHubContext.Clients
                .Group(AuditLogHub.AdminGroupName)
                .SendAsync("AuditLogCreated", payload);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to broadcast audit log {AuditLogId}.", auditLog.Id);
        }
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
            CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
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
