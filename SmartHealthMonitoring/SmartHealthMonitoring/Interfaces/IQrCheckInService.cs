namespace SmartHealthMonitoring.Interfaces;

public interface IQrCheckInService
{
    /// <summary>
    /// Tạo mã Check-in cho hàng đợi (WaitingPatient).
    /// </summary>
    string BuildCheckInCode(int waitingId, int patientId, int doctorId, int sequenceNumber, DateTime acceptedAt);

    /// <summary>
    /// Tạo mã Check-in cho lịch hẹn đã xác nhận (NTF-01 / BOOK-08).
    /// </summary>
    string BuildAppointmentCheckInCode(int appointmentId, int patientId, int doctorId, DateTime slotStart);

    /// <summary>
    /// Sinh ảnh QR PNG từ payload Check-in.
    /// </summary>
    byte[] GeneratePng(string payload, int pixelsPerModule = 8);

    /// <summary>
    /// Sinh ảnh QR dạng data-URI (dùng khi lưu body email / preview).
    /// </summary>
    string GenerateDataUri(string payload, int pixelsPerModule = 8);
}
