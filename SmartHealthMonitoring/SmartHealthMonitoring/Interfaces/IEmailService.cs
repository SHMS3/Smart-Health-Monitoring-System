namespace SmartHealthMonitoring.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlContent);
        string GetHtmlContentFromFile(string templateName, Dictionary<string, string> replacements);
    }
}
