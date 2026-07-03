using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces;

public interface IAppointmentService
{
    /// <summary>Lấy danh sách slot còn trống của bác sĩ theo ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int doctorId, DateOnly date);

    /// <summary>Lấy danh sách slot còn trống của bác sĩ theo khoảng ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsRangeAsync(int doctorId, DateOnly startDate, DateOnly endDate);

    /// <summary>Làm mới toàn bộ slot trong 14 ngày tới sau khi bác sĩ đổi lịch</summary>
    Task<int> RefreshDoctorSlotsAsync(int doctorId);

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

    /// <summary>Tạo yêu cầu đặt lịch hẹn chờ duyệt</summary>
    Task<(bool success, string message, Appointment? appointment)> CreatePendingAppointmentAsync(int slotId, int patientId, string? note);

    /// <summary>Gửi yêu cầu hủy lịch hẹn lên staff</summary>
    Task<bool> RequestCancelAppointmentAsync(int appointmentId, string reason);

    /// <summary>Lấy danh sách lịch hẹn chờ duyệt đặt/hủy cho lễ tân</summary>
    Task<List<Appointment>> GetPendingAppointmentsAsync();

    /// <summary>Phê duyệt yêu cầu đặt lịch</summary>
    Task<bool> ApproveAppointmentBookingAsync(int appointmentId);

    /// <summary>Từ chối yêu cầu đặt lịch</summary>
    Task<bool> RejectAppointmentBookingAsync(int appointmentId);

    /// <summary>Phê duyệt yêu cầu hủy lịch</summary>
    Task<bool> ApproveAppointmentCancellationAsync(int appointmentId);

    /// <summary>Từ chối yêu cầu hủy lịch</summary>
    Task<bool> RejectAppointmentCancellationAsync(int appointmentId);
}
