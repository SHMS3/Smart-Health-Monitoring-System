using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;

namespace SmartHealthMonitoring.Services
{
    public class ChatbotService
    {
        private readonly ChatbotRepository _chatbotRepository;
        private readonly PatientRepository _patientRepository;
        private readonly GeminiService _geminiService;
        public ChatbotService(ChatbotRepository chatbotRepository, GeminiService geminiService, PatientRepository patientRepository)
        {
            _chatbotRepository = chatbotRepository;
            _geminiService = geminiService;
            _patientRepository = patientRepository;
        }

        public async Task<string> SendMessageAsync(int userId,string message)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);

            if (patient == null)
                throw new Exception("Không tìm thấy bệnh nhân");

            var session = await _chatbotRepository.GetLatestSessionAsync(patient.Id);

            if (session == null)
            {
                session = new ChatbotSession
                {
                    PatientId = patient.Id,
                    StartedAt = DateTime.Now
                };

                await _chatbotRepository.CreateSessionAsync(session);
            }

            await _chatbotRepository.CreateMessageAsync(
                    new ChatMessage
                    {
                        SessionId = session.Id,
                        SenderRole = 0,
                        Content = message,
                        SentAt = DateTime.Now
                    });

            var aiResponse = await _geminiService.AskAsync(message);

            await _chatbotRepository.CreateMessageAsync(
                    new ChatMessage
                    {
                        SessionId = session.Id,
                        SenderRole = 1,
                        Content = aiResponse,
                        SentAt = DateTime.Now
                    });

            return aiResponse;
        }


        public async Task<List<ChatbotSession>> GetHistoryAsync(int userId)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);

            if (patient == null)
                throw new Exception("Không tìm thấy bệnh nhân");

            return await _chatbotRepository.GetPatientSessionsAsync(patient.Id);
        }

        public async Task<ChatbotSession?> GetConversationAsync(int sessionId)
        {
            return await _chatbotRepository.GetSessionAsync(sessionId);
        }
    }
}
