using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public byte SenderRole { get; set; }

    public string Content { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public virtual ChatbotSession Session { get; set; } = null!;
}
