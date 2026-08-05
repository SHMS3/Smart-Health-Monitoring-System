using System;

namespace SmartHealthMonitoring.Models;

public class TelemedicineChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    public string MessageContent { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }

    public virtual TelemedicineChatSession Session { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    public virtual User Receiver { get; set; } = null!;
}
