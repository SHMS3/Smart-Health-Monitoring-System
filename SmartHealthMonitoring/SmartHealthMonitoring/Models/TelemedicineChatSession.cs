using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

public class TelemedicineChatSession
{
    public int Id { get; set; }

    public int PatientUserId { get; set; }

    public int? DoctorUserId { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ClaimedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public virtual User PatientUser { get; set; } = null!;
    public virtual User? DoctorUser { get; set; }
    public virtual ICollection<TelemedicineChatMessage> Messages { get; set; } = new List<TelemedicineChatMessage>();
}
