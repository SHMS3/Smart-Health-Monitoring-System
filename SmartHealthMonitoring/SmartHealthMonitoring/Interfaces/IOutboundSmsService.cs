namespace SmartHealthMonitoring.Interfaces;

public interface IOutboundSmsService
{
    Task<bool> SendSmsAsync(string toPhoneNumber, string message);
}
