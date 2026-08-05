namespace SmartHealthMonitoring.Interfaces.Email
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlContent);

        Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlContent,
            IReadOnlyDictionary<string, byte[]>? inlineImages);

        string GetHtmlContentFromFile(string templateName, Dictionary<string, string> replacements);
    }
}
