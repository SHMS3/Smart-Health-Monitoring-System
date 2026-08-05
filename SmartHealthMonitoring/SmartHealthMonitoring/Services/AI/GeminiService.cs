using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Services.AI
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


        public async Task<string> AskAsync(string currentMessage, List<ChatMessage> history, string systemContext = "")
        {
            var apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent"; 

            string promptFilePath = Path.Combine(AppContext.BaseDirectory, "Prompt", "health-assistant.txt");

            if (!File.Exists(promptFilePath))
            {
                throw new FileNotFoundException($"Không tìm thấy file prompt tại: {promptFilePath}");
            }

            string rawPromptTemplate = await File.ReadAllTextAsync(promptFilePath);

            var systemPrompt = rawPromptTemplate.Replace("{{SYSTEM_CONTEXT}}", systemContext);

            var contents = new List<object>();
            string lastRole = "";

            foreach (var msg in history.OrderBy(x => x.SentAt).TakeLast(8))
            {
                string currentRole = msg.SenderRole == 0 ? "user" : "model";

                if (currentRole == lastRole) continue;

                contents.Add(new
                {
                    role = currentRole,
                    parts = new[] { new { text = msg.Content } }
                });

                lastRole = currentRole;
            }

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

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("x-goog-api-key", _apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var error429 = await response.Content.ReadAsStringAsync();

                    throw new Exception($"429 ERROR: {error429}");
                }

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

        public Task<string> GenerateHealthNewsAsync(string statisticsContext)
            => GenerateHealthNewsAsync(statisticsContext, string.Empty);

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
