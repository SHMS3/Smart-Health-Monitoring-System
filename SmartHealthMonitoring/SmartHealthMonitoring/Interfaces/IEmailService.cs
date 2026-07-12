namespace SmartHealthMonitoring.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlContent);

        /// <summary>
        /// Gửi email HTML kèm ảnh inline (cid:...). Key = Content-Id, Value = bytes ảnh (PNG/JPEG).
        /// </summary>
        Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlContent,
            IReadOnlyDictionary<string, byte[]>? inlineImages);

        string GetHtmlContentFromFile(string templateName, Dictionary<string, string> replacements);
    }
}
