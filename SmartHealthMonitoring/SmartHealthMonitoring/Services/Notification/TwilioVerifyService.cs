using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartHealthMonitoring.Interfaces.Notification;
using Twilio;
using Twilio.Rest.Verify.V2.Service;

namespace SmartHealthMonitoring.Services.Notification;

public class TwilioVerifyService : ITwilioVerifyService
{
    private readonly string _serviceSid;
    private readonly ILogger<TwilioVerifyService> _logger;

    public TwilioVerifyService(IConfiguration configuration, ILogger<TwilioVerifyService> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("TwilioSettings");
        var accountSid = section["AccountSid"]!;
        var authToken  = section["AuthToken"]!;
        _serviceSid    = section["VerifyServiceSid"]!;

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task<bool> SendOtpAsync(string toPhoneNumber)
    {
        try
        {
            var phone = NormalizePhone(toPhoneNumber);

            var verification = await VerificationResource.CreateAsync(
                to:          phone,
                channel:     "sms",
                pathServiceSid: _serviceSid
            );

            _logger.LogInformation("Twilio Verify sent to {Phone}. Status: {Status}", phone, verification.Status);
            return verification.Status == "pending";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio Verify SendOtp failed for {Phone}", toPhoneNumber);

            try
            {
                var info = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] SendOtp FAILED\n" +
                           $"Phone: {toPhoneNumber}\n" +
                           $"Error: {ex.Message}\n" +
                           $"Detail: {ex}\n" +
                           $"----------------------------------------\n";
                System.IO.File.AppendAllText("twilio_debug.log", info);
            }
            catch { }

            return false;
        }
    }

    public async Task<bool> VerifyOtpAsync(string toPhoneNumber, string code)
    {
        try
        {
            var phone = NormalizePhone(toPhoneNumber);

            var check = await VerificationCheckResource.CreateAsync(
                to:          phone,
                code:        code,
                pathServiceSid: _serviceSid
            );

            _logger.LogInformation("Twilio Verify check for {Phone}. Status: {Status}", phone, check.Status);
            return check.Status == "approved";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio Verify CheckOtp failed for {Phone}", toPhoneNumber);
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

