namespace SmartHealthMonitoring.Interfaces.QR;

public interface ILocalOcrService
{
    Task<string> ScanCitizenIdAsync(byte[] imageBytes);
}
