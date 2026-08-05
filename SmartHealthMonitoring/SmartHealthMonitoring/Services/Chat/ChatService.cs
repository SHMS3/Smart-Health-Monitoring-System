using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Interfaces.Chat;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services.Chat;

public class ChatService : IChatService
{
    private readonly SmartHealthMonitoringContext _context;

    public ChatService(SmartHealthMonitoringContext context)
    {
        _context = context;
    }


    public async Task<TelemedicineChatSession> GetOrCreateSessionAsync(int patientUserId)
    {
        var existing = await _context.TelemedicineChatSessions
            .Include(s => s.PatientUser)
            .Include(s => s.DoctorUser)
            .Where(s => s.PatientUserId == patientUserId && s.Status <= 1) // 0=Waiting, 1=Active
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
            return existing;

        var session = new TelemedicineChatSession
        {
            PatientUserId = patientUserId,
            Status = 0, // Waiting
            CreatedAt = SmartHealthMonitoring.Common.AppTime.Now
        };

        _context.TelemedicineChatSessions.Add(session);
        await _context.SaveChangesAsync();

        await _context.Entry(session).Reference(s => s.PatientUser).LoadAsync();
        return session;
    }

    public async Task<List<ChatSessionViewModel>> GetWaitingSessionsAsync()
    {
        return await _context.TelemedicineChatSessions
            .Where(s => s.Status == 0) // Waiting
            .OrderBy(s => s.CreatedAt) // FIFO: người đợi lâu nhất lên đầu
            .Include(s => s.PatientUser)
            .Select(s => new ChatSessionViewModel
            {
                SessionId = s.Id,
                PatientUserId = s.PatientUserId,
                PatientName = s.PatientUser.FullName,
                DoctorUserId = null,
                DoctorName = null,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                ClaimedAt = null,
                LastMessage = s.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.MessageContent)
                    .FirstOrDefault(),
                LastMessageTime = s.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => (DateTime?)m.SentAt)
                    .FirstOrDefault(),
                UnreadCount = s.Messages.Count()
            })
            .ToListAsync();
    }

    public async Task<List<ChatSessionViewModel>> GetDoctorSessionsAsync(int doctorUserId)
    {
        return await _context.TelemedicineChatSessions
            .Where(s => s.DoctorUserId == doctorUserId && s.Status >= 1) // Active + Closed
            .OrderByDescending(s => s.Status == 1 ? 1 : 0) // Active lên trước
            .ThenByDescending(s => s.ClaimedAt)
            .Include(s => s.PatientUser)
            .Select(s => new ChatSessionViewModel
            {
                SessionId = s.Id,
                PatientUserId = s.PatientUserId,
                PatientName = s.PatientUser.FullName,
                DoctorUserId = s.DoctorUserId,
                DoctorName = s.DoctorUser != null ? s.DoctorUser.FullName : null,
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                ClaimedAt = s.ClaimedAt,
                LastMessage = s.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.MessageContent)
                    .FirstOrDefault(),
                LastMessageTime = s.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => (DateTime?)m.SentAt)
                    .FirstOrDefault(),
                UnreadCount = s.Messages.Count(m => !m.IsRead && m.SenderId != doctorUserId)
            })
            .ToListAsync();
    }

    public async Task<TelemedicineChatSession?> ClaimSessionAsync(int sessionId, int doctorUserId)
    {
        var session = await _context.TelemedicineChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Status == 0);

        if (session == null)
            return null; // Đã được claim hoặc không tồn tại

        session.DoctorUserId = doctorUserId;
        session.Status = 1; // Active
        session.ClaimedAt = SmartHealthMonitoring.Common.AppTime.Now;

        await _context.SaveChangesAsync();

        await _context.Entry(session).Reference(s => s.PatientUser).LoadAsync();
        await _context.Entry(session).Reference(s => s.DoctorUser).LoadAsync();

        return session;
    }

    public async Task<bool> CloseSessionAsync(int sessionId, int userId)
    {
        var session = await _context.TelemedicineChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.Status == 1);

        if (session == null)
            return false;

        if (session.DoctorUserId != userId && session.PatientUserId != userId)
            return false;

        session.Status = 2; // Closed
        session.ClosedAt = SmartHealthMonitoring.Common.AppTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<TelemedicineChatSession?> GetSessionAsync(int sessionId)
    {
        return await _context.TelemedicineChatSessions
            .Include(s => s.PatientUser)
            .Include(s => s.DoctorUser)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }


    public async Task<List<TelemedicineChatMessage>> GetSessionHistoryAsync(int sessionId)
    {
        return await _context.TelemedicineChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.SentAt)
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .ToListAsync();
    }

    public async Task<TelemedicineChatMessage> SaveMessageAsync(int sessionId, int senderId, string content)
    {
        var session = await _context.TelemedicineChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
            throw new InvalidOperationException("Session không tồn tại.");

        int receiverId;
        if (senderId == session.PatientUserId)
        {
            receiverId = session.DoctorUserId ?? 0; // 0 nếu chưa có bác sĩ (Waiting)
        }
        else
        {
            receiverId = session.PatientUserId;
        }

        var message = new TelemedicineChatMessage
        {
            SessionId = sessionId,
            SenderId = senderId,
            ReceiverId = receiverId,
            MessageContent = content,
            SentAt = SmartHealthMonitoring.Common.AppTime.Now,
            IsRead = false
        };

        _context.TelemedicineChatMessages.Add(message);
        await _context.SaveChangesAsync();

        await _context.Entry(message).Reference(m => m.Sender).LoadAsync();
        await _context.Entry(message).Reference(m => m.Receiver).LoadAsync();

        return message;
    }

    public async Task MarkMessagesAsReadAsync(int sessionId, int userId)
    {
        var unread = await _context.TelemedicineChatMessages
            .Where(m => m.SessionId == sessionId && m.SenderId != userId && !m.IsRead)
            .ToListAsync();

        if (unread.Any())
        {
            foreach (var msg in unread)
            {
                msg.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }
    }
}

