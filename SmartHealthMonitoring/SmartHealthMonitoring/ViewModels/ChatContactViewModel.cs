using System;

namespace SmartHealthMonitoring.ViewModels;

/// <summary>
/// ViewModel hiển thị một liên hệ trong danh sách chat sidebar.
/// </summary>
public class ChatContactViewModel
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public byte Role { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}
