using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Repositories;

public interface IChatbotRepository
{
    Task<ChatbotSession?> GetLatestSessionAsync(int patientId);
    Task<List<ChatbotSession>> GetPatientSessionsAsync(int patientId);
    Task<int> CreateSessionAsync(ChatbotSession session);
    Task CreateMessageAsync(ChatMessage message);
    Task SaveChangesAsync();
    Task<ChatbotSession?> GetSessionAsync(int sessionId);
    Task DeleteSessionAsync(ChatbotSession session);
    Task<PatientHabit?> GetPatientHabitAsync(int patientId);
}
