using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Filters;

public sealed class AuditLogActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    private readonly SmartHealthMonitoringContext _context;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogActionFilter> _logger;

    public AuditLogActionFilter(
        SmartHealthMonitoringContext context,
        IAuditLogService auditLogService,
        ILogger<AuditLogActionFilter> logger)
    {
        _context = context;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var executedContext = await next();

        if (!ShouldLog(context, executedContext))
        {
            return;
        }

        // Các action đã gọi AuditLogService sẽ còn AuditLog trong ChangeTracker.
        // Bỏ qua để mỗi thao tác chỉ có một bản ghi.
        if (_context.ChangeTracker.Entries<AuditLog>().Any())
        {
            return;
        }

        var controller = context.ActionDescriptor.RouteValues["controller"] ?? "Unknown";
        var action = context.ActionDescriptor.RouteValues["action"] ?? context.HttpContext.Request.Method;
        var entityId = FindEntityId(context);
        var roleName = context.HttpContext.User.IsInRole("2") ? "Quản trị viên" : "Bác sĩ";

        try
        {
            await _auditLogService.LogAsync(
                NormalizeAction(action),
                controller,
                entityId,
                $"{roleName} thực hiện thao tác {action} tại {controller}.");
        }
        catch (Exception exception)
        {
            // Audit không được làm hỏng thao tác nghiệp vụ đã hoàn tất.
            _logger.LogError(
                exception,
                "Không thể tự động ghi audit log cho {Controller}/{Action}.",
                controller,
                action);
        }
    }

    private static bool ShouldLog(
        ActionExecutingContext context,
        ActionExecutedContext executedContext)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!user.IsInRole("1") && !user.IsInRole("2"))
        {
            return false;
        }

        if (!MutatingMethods.Contains(context.HttpContext.Request.Method))
        {
            return false;
        }

        if (!context.ModelState.IsValid ||
            (executedContext.Exception is not null && !executedContext.ExceptionHandled))
        {
            return false;
        }

        var statusCode = executedContext.Result switch
        {
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            ObjectResult objectResult => objectResult.StatusCode,
            _ => null
        };

        return statusCode is null or < 400;
    }

    private static string NormalizeAction(string action)
    {
        string[] knownActions =
        [
            "ChangePassword", "GrantAccess", "RevokeAccess", "SendMessage",
            "Deactivate", "Activate", "Unlock", "Lock", "Create", "Update",
            "Delete", "Claim", "Resolve", "Void", "Close", "Reset"
        ];

        return knownActions.FirstOrDefault(
                   known => action.StartsWith(known, StringComparison.OrdinalIgnoreCase))
               ?? action;
    }

    private static string? FindEntityId(ActionExecutingContext context)
    {
        var argument = context.ActionArguments.FirstOrDefault(pair =>
            pair.Key.Equals("id", StringComparison.OrdinalIgnoreCase) ||
            pair.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

        return argument.Value?.ToString()
               ?? context.RouteData.Values["id"]?.ToString();
    }
}
