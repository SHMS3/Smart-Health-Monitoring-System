using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = (config["GeminiApiKey"] ?? throw new Exception("Thiếu GeminiApiKey")).Trim();

        }

        // 3. Hàm quét OCR CCCD bằng Gemini Vision (trả về JSON thô)
        public async Task<string> ScanCitizenIdAsync(byte[] imageBytes, string mimeType)
        {
            var modelUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";
            var base64Data = Convert.ToBase64String(imageBytes);

            var systemPrompt = "Bạn là chuyên gia OCR trích xuất thông tin Căn cước công dân (CCCD) Việt Nam. Hãy đọc kỹ ảnh mặt trước CCCD được cung cấp và trả về dữ liệu định dạng JSON chính xác. Không trả về markdown, không có ```json hay ```. Chỉ trả về chuỗi JSON thô có các trường sau:\n{\n  \"citizenId\": \"12 chữ số\",\n  \"fullName\": \"HỌ VÀ TÊN VIẾT HOA\",\n  \"dateOfBirth\": \"yyyy-MM-dd\",\n  \"sexDisplay\": \"Nam\" hoặc \"Nữ\",\n  \"address\": \"địa chỉ thường trú\"\n}";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = systemPrompt },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = mimeType,
                                    data = base64Data
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, modelUrl);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini OCR failed ({response.StatusCode}): {error}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            var root = document.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var resContent) &&
                    resContent.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp))
                            sb.Append(textProp.GetString());
                    }
                    return sb.ToString();
                }
            }
            throw new Exception("Không thể đọc được thông tin từ ảnh CCCD.");
        }

        public async Task<string> AskAsync(string currentMessage, List<ChatMessage> history, string systemContext = "")
        {
            var apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent"; 

            string promptFilePath = Path.Combine(AppContext.BaseDirectory, "Prompt", "health-assistant.txt");

            if (!File.Exists(promptFilePath))
            {
                throw new FileNotFoundException($"Không tìm thấy file prompt tại: {promptFilePath}");
            }

            // Đọc nội dung file text
            string rawPromptTemplate = await File.ReadAllTextAsync(promptFilePath);

            // 2. NHÚNG DỮ LIỆU ĐỘNG VÀO PROMPT
            var systemPrompt = rawPromptTemplate.Replace("{{SYSTEM_CONTEXT}}", systemContext);

            var contents = new List<object>();
            string lastRole = ""; // Biến để theo dõi role luân phiên

            // Lấy 8 tin nhắn gần nhất để AI nhớ ngữ cảnh
            foreach (var msg in history.OrderBy(x => x.SentAt).TakeLast(8))
            {
                string currentRole = msg.SenderRole == 0 ? "user" : "model";

                // KIỂM TRA BẢO MẬT: Bỏ qua nếu có 2 tin nhắn cùng role liên tiếp (để Gemini không báo lỗi)
                if (currentRole == lastRole) continue;

                contents.Add(new
                {
                    role = currentRole,
                    parts = new[] { new { text = msg.Content } }
                });

                lastRole = currentRole;
            }

            // Nếu lịch sử rỗng (bị lọc hết), ta đẩy câu hỏi hiện tại vào
            if (!contents.Any())
            {
                contents.Add(new { role = "user", parts = new[] { new { text = currentMessage } } });
            }

            var payload = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = contents,
                generationConfig = new { temperature = 0.7, maxOutputTokens = 600 }
            };


            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // TẠO REQUEST VÀ TRUYỀN API KEY QUA HEADER (Chống lỗi URL 404)
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // Bắt riêng lỗi 429 (Too Many Requests)
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var error429 = await response.Content.ReadAsStringAsync();

                    throw new Exception($"429 ERROR: {error429}");
                }

                // Các lỗi khác
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi từ Gemini ({response.StatusCode}): {error}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            var root = document.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var resContent) &&
                    resContent.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    // GIẢI PHÁP: Duyệt qua TẤT CẢ các mảnh (parts) và nối chúng lại với nhau
                    var fullResponse = new StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp))
                        {
                            fullResponse.Append(textProp.GetString());
                        }
                    }

                    return fullResponse.ToString();
                }
            }
            return "Hệ thống AI đang bảo trì, vui lòng thử lại sau.";
        }

        /// <summary>
        /// Generate a health news article (JSON) from statistics context.
        /// Returns raw JSON string: { "title": "...", "summary": "...", "content": "..." }
        /// </summary>
        public Task<string> GenerateHealthNewsAsync(string statisticsContext)
            => GenerateHealthNewsAsync(statisticsContext, string.Empty);

        /// <summary>
        /// Generate a health news article (JSON) from statistics context with a custom user prompt.
        /// Returns raw JSON string: { "title": "...", "summary": "...", "content": "..." }
        /// </summary>
        public async Task<string> GenerateHealthNewsAsync(string statisticsContext, string userPrompt)
        {
            var apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent";

            string promptFilePath = Path.Combine(AppContext.BaseDirectory, "Prompt", "health-news.txt");
            string systemPrompt;
            if (File.Exists(promptFilePath))
                systemPrompt = await File.ReadAllTextAsync(promptFilePath);
            else
                systemPrompt = "Bạn là biên tập viên y tế chuyên nghiệp. Viết bài tin tức sức khỏe bằng tiếng Việt dựa trên dữ liệu thống kê được cung cấp.";

            var promptInstruction = string.IsNullOrWhiteSpace(userPrompt)
                ? "Hãy viết bài tin tức sức khỏe dựa trên dữ liệu trên."
                : $"Hãy viết bài tin tức sức khỏe với chủ đề: \"{userPrompt}\". Dựa trên dữ liệu thống kê để hỗ trợ nội dung.";

            var userMessage = $"Dữ liệu thống kê:\n{statisticsContext}\n\n{promptInstruction}";

            var payload = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = userMessage } } }
                },
                generationConfig = new { temperature = 0.8, maxOutputTokens = 1500 }
            };

            var jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Lỗi từ Gemini ({response.StatusCode}): {error}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = System.Text.Json.JsonDocument.Parse(responseString);
            var root = document.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var resContent) &&
                    resContent.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp))
                            sb.Append(textProp.GetString());
                    }
                    return sb.ToString();
                }
            }
            return string.Empty;
        }
    }
}