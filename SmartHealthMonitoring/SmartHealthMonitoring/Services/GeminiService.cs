namespace SmartHealthMonitoring.Services
{
    public class GeminiService
    {
        public async Task<string> AskAsync(string message)
        {
            await Task.Delay(500);

            return $"AI đã nhận: {message}";
        }
    }
}
