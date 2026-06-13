using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services
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
    }
}
