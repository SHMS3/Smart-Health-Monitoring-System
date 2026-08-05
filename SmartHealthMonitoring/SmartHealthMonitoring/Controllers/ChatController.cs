using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SmartHealthMonitoring.Hubs;
using SmartHealthMonitoring.Interfaces.Chat;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHealthMonitoring.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatController(IChatService chatService, IHubContext<ChatHub> hubContext)
    {
        _chatService = chatService;
        _hubContext = hubContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.CurrentUserId = GetCurrentUserId();
        ViewBag.CurrentUserRole = GetCurrentUserRole();
        ViewBag.CurrentFullName = User.FindFirst("FullName")?.Value ?? "";

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> MySession()
    {
        var userId = GetCurrentUserId();
        var session = await _chatService.GetOrCreateSessionAsync(userId);

        if (session.Status == 0 && (SmartHealthMonitoring.Common.AppTime.Now - session.CreatedAt).TotalSeconds < 5)
        {
            await _hubContext.Clients.Group("Doctors").SendAsync("QueueUpdated");
        }

        return Json(new
        {
            sessionId = session.Id,
            patientUserId = session.PatientUserId,
            patientName = session.PatientUser.FullName,
            doctorUserId = session.DoctorUserId,
            doctorName = session.DoctorUser?.FullName,
            status = session.Status,
            createdAt = session.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            claimedAt = session.ClaimedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        int count = 0;
        if (role == 1) // Doctor: count of distinct active sessions with unread messages + waiting sessions
        {
            var activeSessions = await _chatService.GetDoctorSessionsAsync(userId);
            var activeWithUnread = activeSessions.Count(s => s.Status == 1 && s.UnreadCount > 0);

            var waitingQueue = await _chatService.GetWaitingSessionsAsync();
            var waitingCount = waitingQueue.Count;

            count = activeWithUnread + waitingCount;
        }
        else // Patient: 1 if active session has any unread messages from doctor, else 0
        {
            var session = await _chatService.GetOrCreateSessionAsync(userId);
            var messages = await _chatService.GetSessionHistoryAsync(session.Id);
            var hasUnread = messages.Any(m => m.SenderId != userId && !m.IsRead);
            count = hasUnread ? 1 : 0;
        }

        return Json(new { count });
    }

    [HttpGet]
    public async Task<IActionResult> WaitingQueue()
    {
        var sessions = await _chatService.GetWaitingSessionsAsync();
        return Json(sessions);
    }

    [HttpGet]
    public async Task<IActionResult> DoctorSessions()
    {
        var userId = GetCurrentUserId();
        var sessions = await _chatService.GetDoctorSessionsAsync(userId);
        return Json(sessions);
    }

    [HttpGet]
    public async Task<IActionResult> History(int sessionId)
    {
        var session = await _chatService.GetSessionAsync(sessionId);
        if (session == null)
            return NotFound();

        var userId = GetCurrentUserId();
        if (session.PatientUserId != userId && session.DoctorUserId != userId)
            return Forbid();

        var messages = await _chatService.GetSessionHistoryAsync(sessionId);

        var unreadCount = messages.Count(m => m.SenderId != userId && !m.IsRead);

        await _chatService.MarkMessagesAsReadAsync(sessionId, userId);

        Response.Headers["X-Unread-Count"] = unreadCount.ToString();

        var result = messages.Select(m => new
        {
            id = m.Id,
            sessionId = m.SessionId,
            senderId = m.SenderId,
            senderName = m.Sender.FullName,
            receiverId = m.ReceiverId,
            messageContent = m.MessageContent,
            sentAt = m.SentAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            isRead = m.IsRead
        });

        return Json(result);
    }


    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    private byte GetCurrentUserRole()
    {
        var roleStr = User.FindFirstValue(ClaimTypes.Role);
        return byte.TryParse(roleStr, out byte role) ? role : (byte)0;
    }
}
