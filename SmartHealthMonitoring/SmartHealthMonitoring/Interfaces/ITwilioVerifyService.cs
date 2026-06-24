namespace SmartHealthMonitoring.Interfaces;

public interface ITwilioVerifyService
{
    /// <summary>
    /// Gửi OTP đến số điện thoại qua Twilio Verify API.
    /// </summary>
    Task<bool> SendOtpAsync(string toPhoneNumber);

    /// <summary>
    /// Xác minh mã OTP người dùng nhập.
    /// Trả về true nếu hợp lệ.
    /// </summary>
    Task<bool> VerifyOtpAsync(string toPhoneNumber, string code);
}
