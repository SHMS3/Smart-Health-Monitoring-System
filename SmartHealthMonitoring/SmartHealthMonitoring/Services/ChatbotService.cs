using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;

namespace SmartHealthMonitoring.Services
{
    public class ChatbotService
    {
        private readonly ChatbotRepository _chatbotRepository;
        private readonly PatientRepository _patientRepository;
        private readonly GeminiService _geminiService;
        private readonly DailyVitalLogService _dailyVitalLogService;
        
        public ChatbotService(ChatbotRepository chatbotRepository, GeminiService geminiService, PatientRepository patientRepository, DailyVitalLogService dailyVitalLogService)
        {
            _chatbotRepository = chatbotRepository;
            _geminiService = geminiService;
            _patientRepository = patientRepository;
            _dailyVitalLogService = dailyVitalLogService;
        }

        public async Task<string> SendMessageAsync(int userId, string message)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);
            if (patient == null) throw new Exception("Không tìm thấy bệnh nhân");

            var session = await _chatbotRepository.GetLatestSessionAsync(patient.Id);
            if (session == null || session.StartedAt.Date != DateTime.Today)
            {
                session = new ChatbotSession 
                {
                    PatientId = patient.Id, 
                    StartedAt = DateTime.Now, 
                };
                await _chatbotRepository.CreateSessionAsync(session);
                session.ChatMessages = new List<ChatMessage>();
            }

            // 1. LƯU TIN NHẮN NGƯỜI DÙNG
            await _chatbotRepository.CreateMessageAsync(new ChatMessage
            {
                SessionId = session.Id,
                SenderRole = 0,
                Content = message,
                SentAt = DateTime.Now
            });
            await _chatbotRepository.SaveChangesAsync();

            // =========================================================================
            // 2. TẠO NGỮ CẢNH (CONTEXT) MỚI, CHÍNH XÁC VÀ ĐẦY ĐỦ LUẬT CHƠI CHO AI
            // =========================================================================
            var today = DateTime.Today;
            // Lấy TẤT CẢ log của ngày hôm nay, sắp xếp mới nhất lên đầu
            var logsToday = (await _dailyVitalLogService.GetLogsByDateAsync(userId, today))
                            .OrderByDescending(x => x.LoggedAt)
                            .ToList();

            int logCount = logsToday.Count;
            string systemContext = $"Hôm nay (ngày {today:dd/MM/yyyy}), hệ thống ghi nhận bệnh nhân đã đo chỉ số {logCount} lần.\n";

            if (logCount > 0)
            {
                var latestLog = logsToday.First();
                systemContext += $"- Lần đo gần nhất: lúc {latestLog.LoggedAt:HH:mm} với Huyết áp {latestLog.SystolicBp}/{latestLog.DiastolicBp} mmHg, Nhịp tim {latestLog.HeartRate} bpm.\n";

                // LOGIC CHỐNG SPAM: Cùng khớp với logic 1 tiếng bên Controller của bạn
                var nextAllowedTime = latestLog.LoggedAt.AddHours(1);
                // Nếu bạn đang test 10 giây thì đổi lại thành .AddSeconds(10) nhé!

                if (DateTime.Now < nextAllowedTime)
                {
                    systemContext += $"- TRẠNG THÁI QUAN TRỌNG: Nút ghi log đang bị KHÓA. Bệnh nhân ĐANG TRONG THỜI GIAN CHỜ (Cooldown 1 tiếng). KHÔNG được phép đo tiếp cho đến {nextAllowedTime:HH:mm}. Nếu người dùng đòi đo tiếp, hãy từ chối một cách khéo léo, nhắc họ nghỉ ngơi và quay lại sau mốc giờ đó.\n";
                }
                else
                {
                    systemContext += $"- TRẠNG THÁI: Bệnh nhân CÓ THỂ ghi log tiếp nếu họ muốn.\n";
                }
            }
            else
            {
                systemContext += "- Bệnh nhân CHƯA ghi log lần nào hôm nay. Hãy khuyến khích họ ghi log.\n";
            }
            // =========================================================================

            var history = session.ChatMessages?.ToList() ?? new List<ChatMessage>();

            // 3. Gọi Gemini với Context mới
            var aiResponse = await _geminiService.AskAsync(message, history, systemContext);

            // 4. LƯU TIN NHẮN AI
            await _chatbotRepository.CreateMessageAsync(new ChatMessage
            {
                SessionId = session.Id,
                SenderRole = 3,
                Content = aiResponse,
                SentAt = DateTime.Now
            });
            await _chatbotRepository.SaveChangesAsync();

            return aiResponse;
        }


        public async Task<PagedResult<ChatbotSession>> GetHistoryAsync(int userId, DateTime? fromDate, DateTime? toDate, int pageIndex = 1, int pageSize = 10)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);
            if (patient == null) throw new Exception("Không tìm thấy bệnh nhân");

            var allSessions = await _chatbotRepository.GetPatientSessionsAsync(patient.Id);
            var query = allSessions.AsQueryable();

            // Lọc theo Từ ngày - Đến ngày
            if (fromDate.HasValue)
            {
                query = query.Where(s => s.StartedAt.Date >= fromDate.Value.Date);
            }
            if (toDate.HasValue)
            {
                query = query.Where(s => s.StartedAt.Date <= toDate.Value.Date);
            }

            int totalCount = query.Count();
            var pagedItems = query.OrderByDescending(s => s.StartedAt)
                                  .Skip((pageIndex - 1) * pageSize)
                                  .Take(pageSize)
                                  .ToList();

            return new PagedResult<ChatbotSession>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = pageIndex,
                PageSize = pageSize
            };
        }

        // 2. Thêm hàm xóa cuộc trò chuyện
        public async Task<bool> DeleteConversationAsync(int sessionId, int userId)
        {
            var patient = await _patientRepository.GetByUserIdAsync(userId);
            if (patient == null) return false;

            var session = await _chatbotRepository.GetSessionAsync(sessionId);

            // Kiểm tra session có tồn tại và có đúng là của bệnh nhân này không
            if (session == null || session.PatientId != patient.Id)
                return false;

            await _chatbotRepository.DeleteSessionAsync(session);
            return true;
        }

        public async Task<ChatbotSession?> GetConversationAsync(int sessionId)
        {
            return await _chatbotRepository.GetSessionAsync(sessionId);
        }
    }
}
