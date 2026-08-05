using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.ViewModels;

namespace SmartHealthMonitoring.Interfaces.Chat;

public interface IChatService
{

    Task<TelemedicineChatSession> GetOrCreateSessionAsync(int patientUserId);

    Task<List<ChatSessionViewModel>> GetWaitingSessionsAsync();

    Task<List<ChatSessionViewModel>> GetDoctorSessionsAsync(int doctorUserId);

    Task<TelemedicineChatSession?> ClaimSessionAsync(int sessionId, int doctorUserId);

    Task<bool> CloseSessionAsync(int sessionId, int userId);

    Task<TelemedicineChatSession?> GetSessionAsync(int sessionId);


    Task<List<TelemedicineChatMessage>> GetSessionHistoryAsync(int sessionId);

    Task<TelemedicineChatMessage> SaveMessageAsync(int sessionId, int senderId, string content);

    Task MarkMessagesAsReadAsync(int sessionId, int userId);
}
