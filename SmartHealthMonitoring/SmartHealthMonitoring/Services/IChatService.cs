using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Services;

/// <summary>
/// Service xử lý nghiệp vụ chat Telemedicine theo mô hình hàng đợi.
/// Bệnh nhân tạo session → Bác sĩ claim → Chat 1-1 → Kết thúc.
/// </summary>
public interface IChatService
{
    // ── Session Management ──

    /// <summary>Bệnh nhân: tìm session Active/Waiting hiện tại, nếu không có tạo mới (Waiting).</summary>
    Task<TelemedicineChatSession> GetOrCreateSessionAsync(int patientUserId);

    /// <summary>Bác sĩ: lấy danh sách session đang chờ (Waiting).</summary>
    Task<List<ChatSessionViewModel>> GetWaitingSessionsAsync();

    /// <summary>Bác sĩ: lấy session mình đang xử lý (Active) + đã kết thúc (Closed).</summary>
    Task<List<ChatSessionViewModel>> GetDoctorSessionsAsync(int doctorUserId);

    /// <summary>Bác sĩ nhận phiên: chuyển Waiting → Active, gán DoctorUserId.</summary>
    Task<TelemedicineChatSession?> ClaimSessionAsync(int sessionId, int doctorUserId);

    /// <summary>Kết thúc phiên: chuyển Active → Closed.</summary>
    Task<bool> CloseSessionAsync(int sessionId, int userId);

    /// <summary>Lấy thông tin session theo ID.</summary>
    Task<TelemedicineChatSession?> GetSessionAsync(int sessionId);

    // ── Messages ──

    /// <summary>Lấy lịch sử tin nhắn trong 1 session, ORDER BY SentAt ASC.</summary>
    Task<List<TelemedicineChatMessage>> GetSessionHistoryAsync(int sessionId);

    /// <summary>Lưu tin nhắn mới trong session.</summary>
    Task<TelemedicineChatMessage> SaveMessageAsync(int sessionId, int senderId, string content);

    /// <summary>Đánh dấu tin nhắn đã đọc trong session.</summary>
    Task MarkMessagesAsReadAsync(int sessionId, int userId);
}
