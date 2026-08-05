using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using SmartHealthMonitoring.Models.Configurations;
using System.Text;
using SmartHealthMonitoring.Interfaces.Email;

namespace SmartHealthMonitoring.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _env;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger, IWebHostEnvironment env)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
            _env = env;
        }

        public Task SendEmailAsync(string toEmail, string subject, string htmlContent)
            => SendEmailAsync(toEmail, subject, htmlContent, null);

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlContent,
            IReadOnlyDictionary<string, byte[]>? inlineImages)
        {
            try
            {
                var email = new MimeMessage();
                email.Sender = MailboxAddress.Parse(_emailSettings.SenderEmail);
                email.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlContent };

                if (inlineImages != null)
                {
                    foreach (var (contentId, bytes) in inlineImages)
                    {
                        if (string.IsNullOrWhiteSpace(contentId) || bytes == null || bytes.Length == 0)
                            continue;

                        var resource = builder.LinkedResources.Add(contentId + ".png", bytes);
                        resource.ContentId = contentId;
                        resource.ContentType.MediaType = "image";
                        resource.ContentType.MediaSubtype = "png";
                    }
                }

                email.Body = builder.ToMessageBody();

                using var smtp = new SmtpClient();
                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

                await smtp.ConnectAsync(_emailSettings.MailServer, _emailSettings.MailPort, SecureSocketOptions.StartTls);
                
                await smtp.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);
                
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                _logger.LogDebug($"Email đã gửi thành công tới {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gửi email tới {toEmail}: {ex.Message}");
                throw;
            }
        }

        public string GetHtmlContentFromFile(string templateName, Dictionary<string, string> replacements)
        {
            var templatePath = Path.Combine(_env.WebRootPath, "templates", "emails", templateName);
            if (!System.IO.File.Exists(templatePath))
            {
                _logger.LogWarning($"Không tìm thấy template {templateName} tại {templatePath}");
                return string.Empty;
            }

            var builder = new StringBuilder(System.IO.File.ReadAllText(templatePath));
            foreach (var kvp in replacements)
            {
                builder.Replace(kvp.Key, kvp.Value);
            }
            return builder.ToString();
        }
    }
}

