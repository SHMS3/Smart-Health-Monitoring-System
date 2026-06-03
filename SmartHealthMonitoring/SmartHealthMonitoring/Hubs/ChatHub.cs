using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartHealthMonitoring.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Hubs;

/// <summary>
/// SignalR Hub cho chat Telemedicine theo mô hình hàng đợi.
/// - Bệnh nhân gửi tin nhắn trong session
/// - Bác sĩ claim session từ hàng đợi
/// - Real-time broadcast cho cả 2 bên
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    /// <summary>
    /// Gửi tin nhắn trong một session cụ thể.
    /// Hub tự xác định receiverId từ session.
    /// </summary>
    public async Task SendMessage(int sessionId, string message)
    {
        var senderId = GetUserId();
        if (senderId == 0)
            throw new HubException("Không thể xác định người gửi. Vui lòng đăng nhập lại.");

        if (string.IsNullOrWhiteSpace(message))
            throw new HubException("Nội dung tin nhắn không được để trống.");

        // Kiểm tra quyền: chỉ patient hoặc doctor trong session
        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null)
            throw new HubException("Phiên chat không tồn tại.");

        if (session.PatientUserId != senderId && session.DoctorUserId != senderId)
            throw new HubException("Bạn không thuộc phiên chat này.");

        // Lưu tin nhắn
        var savedMessage = await _chatService.SaveMessageAsync(sessionId, senderId, message.Trim());

        var payload = new
        {
            id = savedMessage.Id,
            sessionId = sessionId,
            senderId = savedMessage.SenderId,
            senderName = savedMessage.Sender.FullName,
            receiverId = savedMessage.ReceiverId,
            messageContent = savedMessage.MessageContent,
            sentAt = savedMessage.SentAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        // Gửi cho chính người gửi
        await Clients.Caller.SendAsync("ReceiveMessage", payload);

        // Gửi cho người nhận (nếu online)
        if (savedMessage.ReceiverId > 0)
        {
            await Clients.User(savedMessage.ReceiverId.ToString()).SendAsync("ReceiveMessage", payload);
        }

        // Nếu bệnh nhân gửi khi đang Waiting → cập nhật queue cho tất cả bác sĩ
        if (session.Status == 0)
        {
            await Clients.Group("Doctors").SendAsync("QueueUpdated");
        }
    }

    /// <summary>
    /// Bác sĩ claim (tiếp nhận) một session từ hàng đợi.
    /// </summary>
    public async Task ClaimSession(int sessionId)
    {
        var doctorUserId = GetUserId();
        if (doctorUserId == 0)
            throw new HubException("Không thể xác định bác sĩ.");

        var session = await _chatService.ClaimSessionAsync(sessionId, doctorUserId);
        if (session == null)
            throw new HubException("Phiên chat đã được tiếp nhận hoặc không tồn tại.");

        var payload = new
        {
            sessionId = session.Id,
            doctorUserId = session.DoctorUserId,
            doctorName = session.DoctorUser?.FullName,
            patientUserId = session.PatientUserId,
            patientName = session.PatientUser.FullName,
            status = session.Status,
            claimedAt = session.ClaimedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        // Thông báo bệnh nhân: phiên đã được tiếp nhận
        await Clients.User(session.PatientUserId.ToString()).SendAsync("SessionClaimed", payload);

        // Thông báo bác sĩ (caller): claim thành công
        await Clients.Caller.SendAsync("SessionClaimed", payload);

        // Cập nhật queue cho tất cả bác sĩ khác
        await Clients.Group("Doctors").SendAsync("QueueUpdated");
    }

    /// <summary>
    /// Kết thúc phiên chat.
    /// </summary>
    public async Task CloseSession(int sessionId)
    {
        var userId = GetUserId();
        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null)
            throw new HubException("Phiên chat không tồn tại.");

        var success = await _chatService.CloseSessionAsync(sessionId, userId);
        if (!success)
            throw new HubException("Không thể kết thúc phiên chat.");

        var payload = new { sessionId = sessionId };

        // Thông báo cả 2 bên
        await Clients.User(session.PatientUserId.ToString()).SendAsync("SessionClosed", payload);
        if (session.DoctorUserId.HasValue)
            await Clients.User(session.DoctorUserId.Value.ToString()).SendAsync("SessionClosed", payload);

        // Cập nhật queue
        await Clients.Group("Doctors").SendAsync("QueueUpdated");
    }

    /// <summary>
    /// Khi kết nối: bác sĩ tự động join group "Doctors" để nhận cập nhật hàng đợi.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var roleClaim = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        if (roleClaim == "1") // Doctor
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Doctors");
        }

        Console.WriteLine($"[ChatHub] User {Context.UserIdentifier} (Role={roleClaim}) connected.");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var roleClaim = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        if (roleClaim == "1")
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Doctors");
        }

        Console.WriteLine($"[ChatHub] User {Context.UserIdentifier} disconnected.");
        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var idStr = Context.UserIdentifier;
        return int.TryParse(idStr, out int id) ? id : 0;
    }
}
