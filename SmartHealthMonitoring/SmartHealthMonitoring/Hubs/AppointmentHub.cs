using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace SmartHealthMonitoring.Hubs;

public class AppointmentHub : Hub
{
    /// <summary>
    /// Khi kết nối, nếu là staff (role 2 hoặc 3) thì tự động join group "Staff"
    /// để nhận broadcast realtime từ AppointmentService.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        Console.WriteLine($"[AppointmentHub] Connected: userId={userId}, role={role}, connectionId={Context.ConnectionId}");

        if (role == "2" || role == "3")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Staff");
            Console.WriteLine($"[AppointmentHub] User {userId} joined group 'Staff'");
        }
        await base.OnConnectedAsync();
    }
}
