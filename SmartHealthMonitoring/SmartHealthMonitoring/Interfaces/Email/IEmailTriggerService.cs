using System.Threading.Tasks;

namespace SmartHealthMonitoring.Interfaces.Email
{
    public interface IEmailTriggerService
    {
        Task<bool> SendAppointmentInvitationAsync(int alertId, int sentByDoctorId, DateTime? appointmentDate = null);

        Task<bool> SendHealthWarningAsync(int patientId, int predictionId);

        Task SendDailyVitalLogReminderAsync(int patientId, string lastLogTimeDisplay);

        Task SendDoctorAcceptedCheckInAsync(int waitingId, int doctorId);

        Task SendBookingConfirmationCheckInAsync(int appointmentId);

        Task SendAppointmentReminderAsync(int appointmentId, string reminderLabel);
    }
}
