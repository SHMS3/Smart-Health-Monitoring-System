using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces;

public interface IAppointmentService
{
    /// <summary>Lấy danh sách slot còn trống của bác sĩ theo ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int doctorId, DateOnly date);

    /// <summary>Lấy danh sách slot còn trống của bác sĩ theo khoảng ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsRangeAsync(int doctorId, DateOnly startDate, DateOnly endDate);

    /// <summary>Giữ chỗ tạm 5 phút trước khi bệnh nhân confirm</summary>
    Task<(bool success, string message)> SoftLockSlotAsync(int slotId, int patientId);

    /// <summary>
    /// Đặt lịch hẹn - OPTIMISTIC CONCURRENCY.
    /// Trả về (true, appointment) nếu thành công,
    /// (false, null) nếu bị người khác giành mất.
    /// </summary>
    Task<(bool success, string message, Appointment? appointment)> BookSlotAsync(int slotId, int patientId, string? note);

    /// <summary>Lấy tất cả lịch hẹn của bệnh nhân</summary>
    Task<List<Appointment>> GetPatientAppointmentsAsync(int patientId);

    /// <summary>Lấy hàng đợi bệnh nhân trong ngày của bác sĩ</summary>
    Task<List<Appointment>> GetDoctorQueueAsync(int doctorId, DateOnly date);

    /// <summary>Bệnh nhân / Bác sĩ huỷ lịch hẹn</summary>
    Task<(bool success, string message)> CancelAppointmentAsync(int appointmentId, int userId, bool isDoctor);

    /// <summary>Liên kết hồ sơ bệnh án khi khám xong → trạng thái Completed</summary>
    Task<bool> CompleteAppointmentAsync(int appointmentId, int clinicalRecordId);

    /// <summary>Block một khoảng thời gian (bác sĩ nghỉ phép)</summary>
    Task BlockTimeAsync(int doctorId, DateTime blockStart, DateTime blockEnd, string? reason);
}
