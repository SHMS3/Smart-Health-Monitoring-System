using Microsoft.EntityFrameworkCore;
using SmartHealthMonitoring.Context;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Repositories
{
    public class ChatbotRepository
    {
        private readonly SmartHealthMonitoringContext _context;

        public ChatbotRepository(SmartHealthMonitoringContext context)
        {
            _context = context;
        }

        public async Task<ChatbotSession?> GetLatestSessionAsync(int patientId)
        {
            return await _context.ChatbotSessions
                .Include(x => x.ChatMessages)
                .Where(x => x.PatientId == patientId)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ChatbotSession>>GetPatientSessionsAsync(int patientId)
        {
            return await _context.ChatbotSessions
                .Where(x => x.PatientId == patientId)
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync();
        }

        public async Task<int>CreateSessionAsync(ChatbotSession session)
        {
            _context.ChatbotSessions.Add(session);

            await _context.SaveChangesAsync();

            return session.Id;
        }

        public async Task CreateMessageAsync(ChatMessage message)
        {
            await _context.ChatMessages.AddAsync(message);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<ChatbotSession?>GetSessionAsync(int sessionId)
        {
            return await _context.ChatbotSessions
                .Include(x => x.ChatMessages)
                .FirstOrDefaultAsync(x => x.Id == sessionId);
        }
    }
}
