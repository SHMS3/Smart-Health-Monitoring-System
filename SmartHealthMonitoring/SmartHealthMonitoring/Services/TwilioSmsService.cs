using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SmartHealthMonitoring.Services;

public class TwilioSmsService : IOutboundSmsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(IConfiguration configuration, ILogger<TwilioSmsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendSmsAsync(string toPhoneNumber, string message)
    {
        var section = _configuration.GetSection("TwilioSettings");
        var accountSid = section["AccountSid"];
        var authToken = section["AuthToken"];
        var fromNumber = section["SmsFromNumber"];
        var messagingServiceSid = section["MessagingServiceSid"];

        if (string.IsNullOrWhiteSpace(accountSid) ||
            string.IsNullOrWhiteSpace(authToken) ||
            (string.IsNullOrWhiteSpace(fromNumber) && string.IsNullOrWhiteSpace(messagingServiceSid)))
        {
            _logger.LogWarning(
                "[SOS SMS] Chưa cấu hình TwilioSettings:AccountSid/AuthToken/SmsFromNumber hoặc MessagingServiceSid. Bỏ qua SMS tới {Phone}. Nội dung: {Message}",
                toPhoneNumber,
                message);
            return false;
        }

        try
        {
            TwilioClient.Init(accountSid, authToken);

            var phone = NormalizePhone(toPhoneNumber);

            MessageResource sentMessage;
            if (!string.IsNullOrWhiteSpace(messagingServiceSid))
            {
                sentMessage = await MessageResource.CreateAsync(
                    to: new PhoneNumber(phone),
                    messagingServiceSid: messagingServiceSid,
                    body: message);
            }
            else
            {
                sentMessage = await MessageResource.CreateAsync(
                    to: new PhoneNumber(phone),
                    from: new PhoneNumber(fromNumber),
                    body: message);
            }

            _logger.LogInformation("[SOS SMS] Đã gửi SMS tới {Phone}. Twilio SID: {Sid}", phone, sentMessage.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SOS SMS] Lỗi khi gửi SMS tới {Phone}", toPhoneNumber);
            return false;
        }
    }

    private static string NormalizePhone(string phone)
    {
        phone = phone.Trim();
        if (phone.StartsWith("+")) return phone;
        if (phone.StartsWith("84") && phone.Length >= 11) return "+" + phone;
        if (phone.StartsWith("0")) return "+84" + phone[1..];
        return "+" + phone;
    }
}
