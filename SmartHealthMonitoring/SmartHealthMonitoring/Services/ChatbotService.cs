using SmartHealthMonitoring.Common;
using SmartHealthMonitoring.Models;
using SmartHealthMonitoring.Repositories;
using SmartHealthMonitoring.ViewModels;

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
            // 3. RAG: INJECT DỮ LIỆU BIỂU ĐỒ 30 NGÀY VÀO CONTEXT ĐỂ AI GIẢI THÍCH
            // =========================================================================
            var trendData = await _dailyVitalLogService.GetPatientHealthTrendsAsync(userId, 30);
            string chartContext = BuildChartRagContext(trendData);
            systemContext += chartContext;
            // =========================================================================

            var history = session.ChatMessages?.ToList() ?? new List<ChatMessage>();

            // 4. Gọi Gemini với Context mới (bao gồm cả dữ liệu biểu đồ)
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


        // =========================================================================
        // RAG BUILDER: Tóm tắt dữ liệu biểu đồ thành văn bản có cấu trúc cho AI
        // =========================================================================
        private string BuildChartRagContext(PersonalHealthTrackerViewModel data)
        {
            if (data == null || data.Labels.Count == 0)
                return "\n[DỮ LIỆU BIỂU ĐỒ]: Bệnh nhân chưa có dữ liệu lịch sử biểu đồ nào trong 30 ngày qua.\n";

            int count = data.Labels.Count;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine();
            sb.AppendLine($"[DỮ LIỆU BIỂU ĐỒ SỨC KHỎE - {data.Days} NGÀY QUA ({count} lần đo)]:");

            // --- Thống kê tổng quan ---
            double avgSys = data.SystolicBpValues.Average();
            double avgDia = data.DiastolicBpValues.Average();
            double avgHr  = data.HeartRateValues.Average();
            int minSys = data.SystolicBpValues.Min();
            int maxSys = data.SystolicBpValues.Max();
            int minDia = data.DiastolicBpValues.Min();
            int maxDia = data.DiastolicBpValues.Max();
            int minHr  = data.HeartRateValues.Min();
            int maxHr  = data.HeartRateValues.Max();

            sb.AppendLine($"- Tổng quan: Huyết áp TB {avgSys:F0}/{avgDia:F0} mmHg | Nhịp tim TB {avgHr:F0} BPM");
            sb.AppendLine($"  + Huyết áp tâm thu: Min={minSys}, Max={maxSys} mmHg");
            sb.AppendLine($"  + Huyết áp tâm trương: Min={minDia}, Max={maxDia} mmHg");
            sb.AppendLine($"  + Nhịp tim: Min={minHr}, Max={maxHr} BPM");

            // --- Phân tích xu hướng (so sánh 1/3 đầu vs 1/3 cuối) ---
            if (count >= 6)
            {
                int slice = count / 3;
                double firstSys = data.SystolicBpValues.Take(slice).Average();
                double lastSys  = data.SystolicBpValues.TakeLast(slice).Average();
                double firstHr  = data.HeartRateValues.Take(slice).Average();
                double lastHr   = data.HeartRateValues.TakeLast(slice).Average();

                string sysTrend = lastSys > firstSys + 5 ? "TĂNG" : (lastSys < firstSys - 5 ? "GIẢM" : "ổn định");
                string hrTrend  = lastHr  > firstHr  + 5 ? "TĂNG" : (lastHr  < firstHr  - 5 ? "GIẢM" : "ổn định");

                sb.AppendLine($"- Xu hướng huyết áp tâm thu: {sysTrend} (giai đoạn đầu TB {firstSys:F0} → giai đoạn cuối TB {lastSys:F0} mmHg)");
                sb.AppendLine($"- Xu hướng nhịp tim: {hrTrend} (giai đoạn đầu TB {firstHr:F0} → giai đoạn cuối TB {lastHr:F0} BPM)");
            }

            // --- Đếm số lần vượt ngưỡng ---
            int dangerSysCount = data.SystolicBpValues.Count(v => v >= 140);
            int dangerDiaCount = data.DiastolicBpValues.Count(v => v >= 90);
            int dangerHrCount  = data.HeartRateValues.Count(v => v > 120 || v < 50);
            int warnSysCount   = data.SystolicBpValues.Count(v => v >= 130 && v < 140);

            sb.AppendLine($"- Số lần huyết áp tâm thu ở mức NGUY HIỂM (≥140 mmHg): {dangerSysCount} lần");
            sb.AppendLine($"- Số lần huyết áp tâm thu ở mức CẢNH BÁO (130-139 mmHg): {warnSysCount} lần");
            sb.AppendLine($"- Số lần huyết áp tâm trương ở mức NGUY HIỂM (≥90 mmHg): {dangerDiaCount} lần");
            sb.AppendLine($"- Số lần nhịp tim bất thường (<50 hoặc >120 BPM): {dangerHrCount} lần");

            // --- 10 điểm dữ liệu gần nhất ---
            int showCount = Math.Min(10, count);
            sb.AppendLine($"- {showCount} lần đo gần nhất (từ cũ đến mới):");
            for (int i = count - showCount; i < count; i++)
            {
                sb.AppendLine($"  [{data.Labels[i]}] HA: {data.SystolicBpValues[i]}/{data.DiastolicBpValues[i]} mmHg | NT: {data.HeartRateValues[i]} BPM");
            }

            sb.AppendLine("(Hãy dùng dữ liệu trên để giải thích biểu đồ khi người dùng hỏi. Không bịa đặt số liệu.)");

            return sb.ToString();
        }
        // =========================================================================

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
