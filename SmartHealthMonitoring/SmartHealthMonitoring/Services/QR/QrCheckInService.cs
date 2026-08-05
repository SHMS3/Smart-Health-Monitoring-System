using QRCoder;
using SmartHealthMonitoring.Interfaces.QR;

namespace SmartHealthMonitoring.Services.QR;

public class QrCheckInService : IQrCheckInService
{
    public string BuildCheckInCode(int waitingId, int patientId, int doctorId, int sequenceNumber, DateTime acceptedAt)
    {
        return string.Join('|',
            "SHMS-CHECKIN",
            $"W{waitingId}",
            $"P{patientId}",
            $"D{doctorId}",
            $"Q{sequenceNumber}",
            acceptedAt.ToString("yyyyMMddHHmm"));
    }

    public string BuildAppointmentCheckInCode(int appointmentId, int patientId, int doctorId, DateTime slotStart)
    {
        return string.Join('|',
            "SHMS-CHECKIN",
            $"A{appointmentId}",
            $"P{patientId}",
            $"D{doctorId}",
            slotStart.ToString("yyyyMMddHHmm"));
    }

    public byte[] GeneratePng(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        return pngQr.GetGraphic(pixelsPerModule);
    }

    public string GenerateDataUri(string payload, int pixelsPerModule = 8)
    {
        var png = GeneratePng(payload, pixelsPerModule);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}

