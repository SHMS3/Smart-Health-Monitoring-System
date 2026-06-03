using System;

namespace SmartHealthMonitoring.Models;

/// <summary>
/// Tin nhắn chat 1-1 giữa Bác sĩ và Bệnh nhân (Telemedicine).
/// Mỗi tin nhắn thuộc về một TelemedicineChatSession.
/// </summary>
public class TelemedicineChatMessage
{
    public int Id { get; set; }

    /// <summary>FK → TelemedicineChatSessions.Id</summary>
    public int SessionId { get; set; }

    /// <summary>FK → Users.Id — Người gửi</summary>
    public int SenderId { get; set; }

    /// <summary>FK → Users.Id — Người nhận</summary>
    public int ReceiverId { get; set; }

    public string MessageContent { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }

    // ── Navigation Properties ──
    public virtual TelemedicineChatSession Session { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    public virtual User Receiver { get; set; } = null!;
}
