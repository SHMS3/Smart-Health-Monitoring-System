using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public partial class ChatbotSession
{
    public int Id { get; set; }

    public int PatientId { get; set; }

    public DateTime StartedAt { get; set; }

    public string? ContextVitals { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual Patient Patient { get; set; } = null!;
}
