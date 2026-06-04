using System.Threading.Tasks;

namespace SmartHealthMonitoring.Services
{
    public interface IEmailTriggerService
    {
        /// <summary>
        /// Gửi thư mời tái khám. Gọi sau khi ResolveAlert thành công.
        /// </summary>
        Task SendAppointmentInvitationAsync(int alertId, int sentByDoctorId);

        /// <summary>
        /// Gửi cảnh báo sức khỏe tự động. Gọi khi AI phát hiện RiskLevel >= 2.
        /// </summary>
        Task SendHealthWarningAsync(int patientId, int predictionId);
    }
}
