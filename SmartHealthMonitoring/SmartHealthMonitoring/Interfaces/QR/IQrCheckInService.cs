namespace SmartHealthMonitoring.Interfaces.QR;

public interface IQrCheckInService
{
    string BuildCheckInCode(int waitingId, int patientId, int doctorId, int sequenceNumber, DateTime acceptedAt);

    string BuildAppointmentCheckInCode(int appointmentId, int patientId, int doctorId, DateTime slotStart);

    byte[] GeneratePng(string payload, int pixelsPerModule = 8);

    string GenerateDataUri(string payload, int pixelsPerModule = 8);
}
