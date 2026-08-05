using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Common;

namespace SmartHealthMonitoring.Interfaces.Chat;

public interface IChatbotService
{
    Task<string> SendMessageAsync(int userId, string message);
    Task<PagedResult<ChatbotSession>> GetHistoryAsync(int userId, DateTime? fromDate, DateTime? toDate, int pageIndex = 1, int pageSize = 10);
    Task<bool> DeleteConversationAsync(int sessionId, int userId);
    Task<ChatbotSession?> GetConversationAsync(int sessionId);
}
