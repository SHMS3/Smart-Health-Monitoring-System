using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.AI;

public interface IGeminiService
{
    Task<string> AskAsync(string currentMessage, List<ChatMessage> history, string systemContext = "");
    Task<string> GenerateHealthNewsAsync(string statisticsContext);
    Task<string> GenerateHealthNewsAsync(string statisticsContext, string userPrompt);
}
