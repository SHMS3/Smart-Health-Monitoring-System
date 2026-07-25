using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces;

public interface IAppointmentService
{
    /// <summary>Lấy danh sách slot còn trống của bác sĩ theo ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int doctorId, DateOnly date, int? currentPatientId = null);

    /// <summary>Lấy danh sách slot còn trống của bác sĩ theo khoảng ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsRangeAsync(int doctorId, DateOnly startDate, DateOnly endDate, int? currentPatientId = null);

    /// <summary>Lấy danh sách slot còn trống của NHIỀU bác sĩ theo khoảng ngày</summary>
    Task<List<AppointmentSlot>> GetAvailableSlotsRangeForDoctorsAsync(List<int> doctorIds, DateOnly startDate, DateOnly endDate, int? currentPatientId = null);


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

    // ═══ SCH-05: Cancel Direct (>= 1 giờ trước) ═══════════════════
    /// <summary>Huỷ lịch trực tiếp nếu còn >= 1h trước giờ hẹn, nhả slot + SignalR</summary>
    Task<(bool success, string message)> CancelDirectAsync(int appointmentId, int patientId);

    // ═══ SCH-06: Reschedule ════════════════════════════════════════
    /// <summary>Dời lịch: Transaction huỷ slot cũ + lock slot mới đồng thời</summary>
    Task<(bool success, string message, Appointment? newAppointment)> RescheduleAppointmentAsync(
        int appointmentId, int newSlotId, int patientId);

    // ═══ SCH-07: Waitlist ══════════════════════════════════════════
    /// <summary>Đăng ký nhận thông báo khi có slot trống</summary>
    Task<(bool success, string message)> JoinWaitlistAsync(int patientId, int doctorId, DateOnly watchDate);

    /// <summary>Lấy danh sách waitlist của bệnh nhân</summary>
    Task<List<AppointmentWaitlist>> GetPatientWaitlistAsync(int patientId);

    /// <summary>Huỷ đăng ký waitlist</summary>
    Task<bool> RemoveFromWaitlistAsync(int waitlistId, int patientId);

    /// <summary>Thông báo email cho waitlist subscribers khi slot được nhả</summary>
    Task NotifyWaitlistSubscribersAsync(int doctorId, DateOnly date);
}
