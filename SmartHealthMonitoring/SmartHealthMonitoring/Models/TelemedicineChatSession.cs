using System;
using System.Collections.Generic;

namespace SmartHealthMonitoring.Models;

/// <summary>
/// Phiên chat Telemedicine giữa Bệnh nhân và Bác sĩ.
/// Bệnh nhân tạo session (Waiting) → Bác sĩ claim (Active) → Kết thúc (Closed).
/// </summary>
public class TelemedicineChatSession
{
    public int Id { get; set; }

    /// <summary>FK → Users.Id — Bệnh nhân tạo phiên</summary>
    public int PatientUserId { get; set; }

    /// <summary>FK → Users.Id — Bác sĩ tiếp nhận (null khi đang đợi)</summary>
    public int? DoctorUserId { get; set; }

    /// <summary>Trạng thái: 0 = Waiting, 1 = Active, 2 = Closed</summary>
    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm bác sĩ nhận phiên</summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>Thời điểm kết thúc phiên</summary>
    public DateTime? ClosedAt { get; set; }

    // ── Navigation Properties ──
    public virtual User PatientUser { get; set; } = null!;
    public virtual User? DoctorUser { get; set; }
    public virtual ICollection<TelemedicineChatMessage> Messages { get; set; } = new List<TelemedicineChatMessage>();
}
