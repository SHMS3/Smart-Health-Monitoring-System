using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SmartHealthMonitoring.Hubs;

[Authorize(Roles = "2")]
public class AuditLogHub : Hub
{
    public const string AdminGroupName = "AuditLogAdmins";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.FindFirst(ClaimTypes.Role)?.Value == "2")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroupName);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroupName);
        await base.OnDisconnectedAsync(exception);
    }
}
