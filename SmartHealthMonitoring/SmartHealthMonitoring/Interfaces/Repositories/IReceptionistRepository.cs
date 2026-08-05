using SmartHealthMonitoring.Models;

namespace SmartHealthMonitoring.Interfaces.Repositories;

public interface IReceptionistRepository
{
        Microsoft.EntityFrameworkCore.DbContext GetContext();
    Task<int> GetPendingPaymentsCountAsync();
    Task<List<Payment>> GetPendingPaymentsAsync(int page, int pageSize);
    Task<int> GetPaidPaymentsCountAsync(DateTime start, DateTime end);
    Task<List<Payment>> GetPaidPaymentsAsync(DateTime start, DateTime end, int page, int pageSize);
    Task<Payment?> GetPaymentDetailsAsync(int id);
    Task<Payment?> GetPaymentByIdAsync(int id);
    Task UpdatePaymentAsync(Payment payment);
    Task<List<Payment>> GetPendingPaymentsListAsync();
    IQueryable<SmartHealthMonitoring.Models.Patient> GetPatientsQuery(string? search);
    Task<int> GetPatientsCountAsync(string? search);
    Task<List<SmartHealthMonitoring.Models.Patient>> GetPatientsAsync(string? search, int page, int pageSize);
    Task<SmartHealthMonitoring.Models.Patient?> GetPatientByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> PhoneExistsAsync(string phone);
    Task<bool> CitizenIdExistsAsync(string citizenId);
    Task AddUserAsync(User user);
    Task AddPatientAsync(SmartHealthMonitoring.Models.Patient patient);
    Task<bool> IsPatientInWaitingListAsync(int patientId);
    Task<AppointmentSlot?> GetAvailableSlotAsync(int slotId, int doctorId);
    Task AddAppointmentAsync(SmartHealthMonitoring.Models.Appointment appointment);
    Task<int> GetMaxSequenceNumberTodayAsync(int doctorId, DateTime todayUtc);
    Task AddWaitingPatientAsync(WaitingPatient waitingPatient);
    Task<bool> HasSlotsForDateAsync(DateTime todayUtc, DateTime tomorrowUtc);
    Task<List<dynamic>> GetDoctorsWithSlotsAsync(DateTime todayUtc, DateTime tomorrowUtc);
    Task<List<dynamic>> GetDoctorSlotsAsync(int doctorId, DateTime nowUtc, DateTime tomorrowUtc);
    Task<List<DoctorWorkSchedule>> GetWorkSchedulesByDayAsync(int dayOfWeek);
    Task<bool> SlotExistsAsync(int doctorId, DateTime slotStartUtc);
    Task AddAppointmentSlotAsync(AppointmentSlot slot);
    Task SaveChangesAsync();
}



