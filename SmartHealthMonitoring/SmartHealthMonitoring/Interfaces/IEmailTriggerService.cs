using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces
{
    public interface IEmailTriggerService
    {
        /// <summary>
        /// Gửi thư mời tái khám. Gọi sau khi ResolveAlert thành công.
        /// </summary>
        Task SendAppointmentInvitationAsync(int alertId, int sentByDoctorId, DateTime? appointmentDate = null);

        /// <summary>
        /// Gửi cảnh báo sức khỏe tự động. Gọi khi AI phát hiện RiskLevel >= 2.
        /// </summary>
        Task SendHealthWarningAsync(int patientId, int predictionId);

        /// <summary>
        /// Gửi email nhắc nhở bệnh nhân ghi log chỉ số sinh hiệu hàng ngày.
        /// </summary>
        Task SendDailyVitalLogReminderAsync(int patientId, string lastLogTimeDisplay);

        /// <summary>
        /// Gửi email QR Check-in khi bác sĩ tiếp nhận bệnh nhân trong hàng đợi thành công.
        /// </summary>
        Task SendDoctorAcceptedCheckInAsync(int waitingId, int doctorId);

        /// <summary>
        /// NTF-01: Email xác nhận đặt lịch + QR Check-in khi BOOK-08 (duyệt lịch) thành công.
        /// </summary>
        Task SendBookingConfirmationCheckInAsync(int appointmentId);

        /// <summary>
        /// NTF-02: Email nhắc lịch hẹn (24h hoặc 2h trước giờ khám).
        /// </summary>
        Task SendAppointmentReminderAsync(int appointmentId, string reminderLabel);
    }
}
