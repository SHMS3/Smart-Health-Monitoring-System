using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Appointment;

public interface IAppointmentService
{
    Task<List<AppointmentSlot>> GetAvailableSlotsAsync(int doctorId, DateOnly date, int? currentPatientId = null);

    Task<List<AppointmentSlot>> GetAvailableSlotsRangeAsync(int doctorId, DateOnly startDate, DateOnly endDate, int? currentPatientId = null);

    Task<List<AppointmentSlot>> GetAvailableSlotsRangeForDoctorsAsync(List<int> doctorIds, DateOnly startDate, DateOnly endDate, int? currentPatientId = null);


    Task<(bool success, string message)> SoftLockSlotAsync(int slotId, int patientId);

    Task<(bool success, string message, SmartHealthMonitoring.Models.Appointment? appointment)> BookSlotAsync(int slotId, int patientId, string? note);

    Task<List<SmartHealthMonitoring.Models.Appointment>> GetPatientAppointmentsAsync(int patientId);

    Task<List<SmartHealthMonitoring.Models.Appointment>> GetDoctorQueueAsync(int doctorId, DateOnly date);

    Task<(bool success, string message)> CancelAppointmentAsync(int appointmentId, int userId, bool isDoctor);

    Task<bool> CompleteAppointmentAsync(int appointmentId, int clinicalRecordId);

    Task BlockTimeAsync(int doctorId, DateTime blockStart, DateTime blockEnd, string? reason);

    Task<(bool success, string message, SmartHealthMonitoring.Models.Appointment? appointment)> CreatePendingAppointmentAsync(int slotId, int patientId, string? note);

    Task<bool> RequestCancelAppointmentAsync(int appointmentId, string reason);

    Task<List<SmartHealthMonitoring.Models.Appointment>> GetPendingAppointmentsAsync();

    Task<bool> ApproveAppointmentBookingAsync(int appointmentId);

    Task<bool> RejectAppointmentBookingAsync(int appointmentId);

    Task<bool> ApproveAppointmentCancellationAsync(int appointmentId);

    Task<bool> RejectAppointmentCancellationAsync(int appointmentId);

    Task<(bool success, string message)> CancelDirectAsync(int appointmentId, int patientId);

    Task<(bool success, string message, SmartHealthMonitoring.Models.Appointment? newAppointment)> RescheduleAppointmentAsync(
        int appointmentId, int newSlotId, int patientId);

    Task<(bool success, string message)> JoinWaitlistAsync(int patientId, int doctorId, DateOnly watchDate);

    Task<List<AppointmentWaitlist>> GetPatientWaitlistAsync(int patientId);

    Task<bool> RemoveFromWaitlistAsync(int waitlistId, int patientId);

    Task NotifyWaitlistSubscribersAsync(int doctorId, DateOnly date);

    Task<AppointmentSlot?> GetSlotByIdAsync(int slotId);
    Task<bool> HasActiveOrPendingAppointmentAsync(int patientId);
    Task<SmartHealthMonitoring.Models.Appointment?> GetAppointmentByIdAndPatientAsync(int appointmentId, int patientId);
    Task<List<SmartHealthMonitoring.Models.Appointment>> GetDoctorCalendarAppointmentsAsync(int doctorId, DateTime startDate, DateTime endDate);
    Task<List<WaitingPatient>> GetDoctorWaitingQueueAsync(int doctorId, DateTime date);
    Task<List<int>> GetPatientPaymentsAsync(List<int> patientIds, DateTime date, string status);
}

