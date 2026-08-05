namespace SmartHealthMonitoring.Interfaces.Notification;

public interface ITwilioVerifyService
{
    Task<bool> SendOtpAsync(string toPhoneNumber);

    Task<bool> VerifyOtpAsync(string toPhoneNumber, string code);
}
