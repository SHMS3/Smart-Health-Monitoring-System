using System;

namespace SmartHealthMonitoring.ViewModels;

/// <summary>
/// ViewModel hiển thị thông tin một phiên chat trong hàng đợi hoặc danh sách.
/// </summary>
public class ChatSessionViewModel
{
    public int SessionId { get; set; }
    public int PatientUserId { get; set; }
    public string PatientName { get; set; } = null!;
    public int? DoctorUserId { get; set; }
    public string? DoctorName { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}
